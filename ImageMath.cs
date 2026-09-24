using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PrintScroller
{
    internal static class ImageMath
    {
        public static byte[] ToGrayBuffer(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var gray = new byte[w * h];
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = data.Stride;
                var buffer = new byte[stride * h];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < h; y++)
                {
                    int rowStart = y * stride;
                    int outRow = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int idx = rowStart + x * 3;
                        byte b = buffer[idx];
                        byte g = buffer[idx + 1];
                        byte r = buffer[idx + 2];
                        gray[outRow + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return gray;
        }

        /// <summary>
        /// Estima em quantos pixels o conteúdo de "current" já rolou para baixo em relação
        /// a "reference", comparando uma faixa horizontal do topo de "current" com a mesma
        /// faixa em diferentes profundidades de "reference". Retorna 0 se nenhum
        /// deslocamento confiável for encontrado.
        ///
        /// A busca é feita pixel a pixel (não em passos grosseiros): texto tem detalhe
        /// fino, e um candidato errado por 1-3px já produz erro bem mais alto — testar só
        /// múltiplos de "searchStep" e refinar depois pode rejeitar um match verdadeiro
        /// antes mesmo de chegar perto o bastante para refinar (o ponto da grade mais
        /// próximo do alinhamento real já falha o limite de erro sozinho).
        ///
        /// Outros cuidados, descobertos testando contra captura de tela real e conteúdo
        /// com texto repetitivo:
        /// (1) faixas quase em branco (entrelinhas, margens) "combinam" com quase qualquer
        ///     coisa por terem variância baixa, então são rejeitadas de saída;
        /// (2) se o topo de "current" já bate com o topo de "reference" sem nenhum
        ///     deslocamento, nada rolou de verdade — checa isso primeiro para não ficar
        ///     "preso" recasando o mesmo conteúdo parado indefinidamente;
        /// (3) texto com espaçamento de linha regular (ou parágrafos parecidos) cria falsos
        ///     candidatos — por isso exigimos que uma SEGUNDA faixa, separada da primeira
        ///     dentro do quadro, também bata no mesmo deslocamento antes de aceitar.
        /// </summary>
        public static int EstimateOffset(byte[] referenceGray, byte[] currentGray, int width, int height,
            int columnStep, int bandHeight, int errorThreshold)
        {
            int maxOffset = height - bandHeight;
            if (maxOffset < 1) return 0;

            if (!HasEnoughContrast(currentGray, width, 0, bandHeight, columnStep)) return 0;

            // nada mudou desde a referência: não há o que costurar.
            double noMotionError = BandError(referenceGray, 0, currentGray, 0, width, bandHeight, columnStep);
            if (noMotionError <= NoMotionThreshold) return 0;

            var errors = new double[maxOffset + 1];
            double bestError = double.MaxValue;
            for (int d = 1; d <= maxOffset; d++)
            {
                double error = BandError(referenceGray, d, currentGray, 0, width, bandHeight, columnStep);
                errors[d] = error;
                if (error < bestError) bestError = error;
            }
            if (bestError > errorThreshold) return 0;

            // dentre os candidatos praticamente empatados com o melhor (mesmíssimo
            // alinhamento, dentro do ruído de antialiasing), fica com o menor
            // deslocamento — evita "pular" para um múltiplo do espaçamento de linha.
            // A margem é pequena de propósito: folga demais aqui deixa passar um
            // candidato desalinhado por 1-2px, que em textos pequenos de UI já
            // produz costura visivelmente embaralhada (linha duplicada ou pulada).
            double acceptError = bestError + 0.75;
            int chosen = 0;
            for (int d = 1; d <= maxOffset; d++)
            {
                if (errors[d] <= acceptError) { chosen = d; break; }
            }
            if (chosen <= 0) return 0;

            if (!ConfirmWithSecondBand(referenceGray, currentGray, width, height, chosen, bandHeight, columnStep, errorThreshold))
                return 0;

            return chosen;
        }

        /// <summary>
        /// Reforça a confiança no deslocamento candidato comparando uma segunda faixa,
        /// separada da primeira, no mesmo deslocamento. O espaço disponível para essa
        /// segunda faixa encolhe conforme o deslocamento cresce (a sobreposição entre
        /// "current" e "reference" é menor); por isso a posição da faixa é escolhida
        /// dinamicamente, usando o máximo de separação que ainda couber, em vez de uma
        /// posição fixa. Isso mantém a verificação ativa mesmo em rolagens rápidas
        /// (deslocamentos grandes) — exatamente quando ela é mais necessária. Só quando
        /// nem uma separação mínima cabe (deslocamento já quase no limite físico de
        /// busca) é que se confia na primeira faixa sozinha.
        /// </summary>
        private static bool ConfirmWithSecondBand(byte[] referenceGray, byte[] currentGray, int width, int height,
            int offset, int bandHeight, int columnStep, int errorThreshold)
        {
            const int PreferredGap = 40;
            const int MinGap = 8;

            int maxProbeRow = height - offset - bandHeight;
            int minProbeRow = bandHeight + MinGap;
            if (maxProbeRow < minProbeRow) return true;

            int probeRow = Math.Min(bandHeight + PreferredGap, maxProbeRow);
            if (!HasEnoughContrast(currentGray, width, probeRow, bandHeight, columnStep)) return true;

            double error = BandError(referenceGray, probeRow + offset, currentGray, probeRow, width, bandHeight, columnStep);
            return error <= errorThreshold;
        }

        private const int NoMotionThreshold = 6;

        private static bool HasEnoughContrast(byte[] gray, int width, int rowStart, int bandHeight, int columnStep)
        {
            byte min = 255, max = 0;
            for (int y = 0; y < bandHeight; y++)
            {
                int row = (rowStart + y) * width;
                for (int x = 0; x < width; x += columnStep)
                {
                    byte v = gray[row + x];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            return (max - min) >= 20;
        }

        private static double BandError(byte[] referenceGray, int refRowStart, byte[] currentGray, int curRowStart,
            int width, int bandHeight, int columnStep)
        {
            long sum = 0;
            int count = 0;
            for (int y = 0; y < bandHeight; y++)
            {
                int refRow = (refRowStart + y) * width;
                int curRow = (curRowStart + y) * width;
                for (int x = 0; x < width; x += columnStep)
                {
                    sum += Math.Abs(referenceGray[refRow + x] - currentGray[curRow + x]);
                    count++;
                }
            }
            return count == 0 ? double.MaxValue : (double)sum / count;
        }

        public static Bitmap CropBottom(Bitmap source, int height)
        {
            height = Math.Min(height, source.Height);
            var srcRect = new Rectangle(0, source.Height - height, source.Width, height);
            var result = new Bitmap(source.Width, height, PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(result);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, height), srcRect, GraphicsUnit.Pixel);
            return result;
        }

        public static Bitmap StitchVertically(List<Bitmap> segments, int width)
        {
            int totalHeight = 0;
            foreach (var s in segments) totalHeight += s.Height;

            var result = new Bitmap(width, Math.Max(totalHeight, 1), PixelFormat.Format24bppRgb);
            using var g = Graphics.FromImage(result);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

            int y = 0;
            foreach (var s in segments)
            {
                g.DrawImage(s, new Rectangle(0, y, s.Width, s.Height));
                y += s.Height;
            }
            return result;
        }
    }
}
