# PrintScroller

Ferramenta Windows para tirar prints verticais rolávois (scrolling screenshots).

## Como funciona

1. Ao iniciar, uma sobreposição semitransparente cobre a tela: arraste com o mouse para
   delimitar a área a ser capturada e solte o botão (ESC cancela).
2. Um print da área inteira é tirado automaticamente assim que a área é delimitada.
3. Role o conteúdo normalmente (mouse, teclado, barra de rolagem) dentro da área.
   A cada ~120ms a ferramenta compara a área atual com o último quadro de referência.
4. Assim que pixels novos e confiáveis aparecem na parte de baixo da área (ou seja,
   o conteúdo rolou), essa fatia nova é costurada embaixo do print anterior. Isso se
   repete continuamente — não é preciso esperar uma "página inteira" rolar; cada
   pedacinho novo detectado já vai sendo costurado, então o resultado final é o
   mesmo de costurar páginas inteiras, só que de forma muito mais confiável.
5. Pressione **ENTER** a qualquer momento (mesmo com foco em outra janela) para
   finalizar. Funciona com a mesma lógica do passo 4: pega o que houver de novo
   desde a última costura, mesmo que seja uma rolagem incompleta, e finaliza.
6. Uma caixa de diálogo pede onde salvar o PNG final.

Um log de diagnóstico é escrito em `%TEMP%\printscroller_debug.log` a cada captura
(deslocamento detectado por tick), útil para investigar problemas futuros.

## Compilar

Requer .NET SDK 8.0+.

```
dotnet build -c Release
```

Para gerar um executável único, sem depender do .NET instalado na máquina:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

O executável fica em `publish\PrintScroller.exe`.

## Rodar

```
dotnet run -c Release
```

ou execute diretamente `publish\PrintScroller.exe`.

## Limitações conhecidas

- A detecção de rolagem é feita por comparação de pixels (luminância) entre o
  último quadro de referência e o quadro atual, a cada ~120ms. Se a rolagem entre
  duas verificações ultrapassar a altura inteira da área selecionada (por exemplo,
  um scroll muito brusco de trackpad, ou pressionar "Fim"/"Page Down" repetidas
  vezes muito rápido), não sobra nenhum pixel em comum entre os dois quadros para
  comparar — isso é uma limitação física de qualquer método baseado em comparação
  de imagem, não só deste. Role em ritmo contínuo e moderado (como um scroll normal
  de leitura) para melhor resultado; a ferramenta tolera bem rolagens de até quase
  a altura da área por vez.
- Áreas com fundo muito uniforme (ex.: um trecho totalmente branco, sem texto) são
  ignoradas até que apareça conteúdo com contraste suficiente para comparar.
- Não há suporte a rolagem horizontal.

Testado com uma bateria de testes automatizados (Playwright dirigindo um navegador
real + simulação de mouse/teclado do Windows) cobrindo rolagem gradual contínua,
período parado (sem rolagem) e ENTER pressionado no meio de uma rolagem ativa.
