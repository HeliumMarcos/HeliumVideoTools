# Desempenho do aplicativo instalado — 3.0.14

Medição local em 02/11/2026 do executável self-contained/ReadyToRun já instalado, versão `3.0.14+22931bf2605fed0c3486cd99b5d960ecb548b029`, runtime incluído .NET 10.0.14. Não é a janela preview nem o motor dentro do host de testes. O cliente externo apenas dirige a UI e amostra processos; sua própria memória não integra os totais do aplicativo.

Registros sem caminhos pessoais: [medição bruta](performance/installed-3.0.14.json) e [conferência das saídas](performance/installed-3.0.14-output-checks.json). Hardware: Ryzen 7 3700X/16 processadores lógicos, Windows build 26300 x64. FFmpeg/ffprobe instalados 9.0.2 Gyan Essentials; hashes e runtime no JSON. Não havia conjunto de ferramentas atualizado no perfil; o par embutido foi usado.

## Abertura real e memória

Seis processos novos da instalação normal, com preferências, recuperação e consulta automática habilitadas. Ponto de chegada: a UI Automation consegue encontrar o título Compactar vídeos e a ação principal habilitada. Memória observada 500 ms depois. A sondagem externa usa espera de 100 ms: os números têm essa resolução aproximada, acrescida do custo das consultas; não são medidas exatas do instante ContentRendered.

| Medida | Resultado |
| --- | ---: |
| Primeira amostra da série final | 790 ms |
| Cinco seguintes | 718–734 ms |
| Mediana das cinco seguintes | **727 ms** |
| Memória residente média nas seis observações | **136,8 MB** |

Todas são aberturas observadas com cache potencialmente aquecido. Houve tentativas anteriores para ajustar a seleção no diálogo nativo, sem executar operações de vídeo; por isso nem a primeira amostra da série final deve ser tratada como fria. Não houve reinicialização, esvaziamento de cache, alteração de energia, escala ou encerramento de programas do usuário. Abertura fria controlada permanece não medida. Não comparar esses tempos/memória com hosts antigos de teste, que tinham outro isolamento/runtime/método.

## Complemento após reinício — 02/11/2026

Após o usuário informar que reiniciou o computador, uma nova série de seis aberturas da instalação 3.0.14 terminou sem erro. A primeira abertura observada levou **1.306,7 ms**; as cinco seguintes, **639,8–705,2 ms**, com mediana **646,8 ms**. Memória residente da primeira observação: **142,7 MB**, amostrada 500 ms após a ação ficar disponível. [Registro bruto](performance/installed-3.0.14-after-restart.json).

O Windows tinha aproximadamente 998 segundos de atividade na primeira observação. Não houve auditoria de possíveis aberturas anteriores desde o boot, limpeza de cache ou controle de prefetch/carga externa: esta é uma observação após reinício, não uma garantia de cache frio. O cliente encerrou suas próprias sessões e restaurou as preferências; SHA-256 do perfil foi conferido contra o backup. Nenhuma operação de mídia foi repetida. A verificação de abertura após reinício encerra o item 5 no escopo combinado, com essa limitação registrada.

## Processamento pela interface instalada

Fonte artificial: 180 segundos, 1280×720/25 fps, H.264 ultrafast CRF 12 com áudio AAC, **249.981.084 bytes**. São quatro vezes a duração/tamanho aproximado da amostra anterior de 45 s. A amostra foi gerada uma vez e copiada para a pasta nova da série final; o hash permaneceu igual após as operações.

| Operação | Tempo observado | Pico residente simultâneo app + filhos | Tamanho de saída |
| --- | ---: | ---: | ---: |
| H.264, duas etapas, áudio original, alvo 98 MB | **18,28 s** | **673,1 MB** | **92,25 MB** |
| Três partes precisas H.264/AAC | **12,78 s** | **655,3 MB** | **82,97 MB somados** |
| WebM VP8/Opus, duas etapas | **59,45 s** | **401,5 MB** | **188,94 MB** |

Uma execução por operação. A cronometragem começa antes de invocar a ação e termina quando ela fica habilitada após concluir o lote; inclui validação, processamento e relatório da interface. Preparação/seleção da amostra e sondagens de conferência posteriores ficam fora. A divisão por quantidade não impõe teto em MB. WebM usou teto aplicado de **244,981 MB** (menor entre 299 MB e 98% do original); a saída permaneceu abaixo desse orçamento e do original.

O sampler percorre a árvore de processos pelo PID do app e soma memória residente/privada dos processos presentes no mesmo instante. As três operações chegaram a três processos simultâneos. Intervalo nominal 150 ms mais custo das consultas: picos curtos/processos breves podem não ser capturados. Páginas compartilhadas podem ser contadas mais de uma vez, portanto soma residente não equivale a RAM física exclusiva. Picos de bytes privados simultâneos: 747,8/676,0/349,8 MB, respectivamente; os instantes dos picos residente/privado podem ser diferentes.

## Verificações e proteção dos dados

O cliente de medição e a suíte passaram no GitHub Actions em [41499838959](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41499838959), para o commit `08baaa2`. Os tempos e a memória deste relatório foram medidos localmente na instalação; a execução no GitHub valida os testes, sem substituir essas medições.

- Cliente recusou sessão do aplicativo já aberta e controlou somente os processos que iniciou. Nenhum vídeo do usuário foi selecionado.
- O diálogo nativo exigiu seleção assistida da amostra: identificadores de controles se repetem e variam entre provedores. Essa etapa não integra os tempos de processamento. O teste valida a quantidade/nome e o caminho de ajuda acessível quando exposto antes de iniciar.
- Cada saída foi sondada: áudio presente, duração total com diferença menor que um segundo e tetos aplicáveis respeitados. Os três trechos têm aproximadamente 60 s cada.
- Inspeção adicional confirmou H.264/AAC nos MP4, VP8/Opus no WebM e hash SHA-256 igual dos pacotes AAC copiados no MP4 compactado e no original.
- Fonte preservada por SHA-256. Preferências restauradas byte a byte a partir de backup privado em work após fechar a sessão de teste; hash conferido. O backup e arquivos de mídia não acompanham o Git.
- Teste focado do sampler inicia um filho próprio, confirma inclusão no total simultâneo e encerra somente esse filho. Não é benchmark de desempenho adicional.

Não houve mudança de produção, tuning de codecs ou alegação de aceleração. Uma amostra por operação não é uma distribuição estatística; carga externa/cache/temperatura não foram controlados. Mídia artificial de três minutos não comprova desempenho ou aceitação de audiências reais extensas. Os números estabelecem uma referência da instalação normal.

## Reproduzir

Feche o aplicativo antes. O cliente recusa uma sessão existente. Com o SDK local e as ferramentas instaladas:

```powershell
.\work\tools\dotnet\dotnet.exe run --project tests/HeliumVideoTools.Tests -c Release -- --benchmark-installed "$env:LOCALAPPDATA\Programs\HeliumVideoTools\app\HeliumVideoTools.exe" work/installed-performance
```

O comando cria uma pasta nova, mede seis aberturas, gera a fonte e mostra o caminho exato a selecionar no diálogo. Só selecione essa fonte. Cria saídas novas ao lado dela. Pode receber como argumento final uma fonte sintética anterior conhecida, copiando-a para a pasta nova. Não apontar para audiências reais.

Para uma futura observação após reiniciar o computador por decisão do usuário, sem gerar/processar mídia novamente:

```powershell
.\work\tools\dotnet\dotnet.exe run --project tests/HeliumVideoTools.Tests -c Release -- --benchmark-installed-startup "$env:LOCALAPPDATA\Programs\HeliumVideoTools\app\HeliumVideoTools.exe" work/installed-startup-after-restart
```

Esse modo também registra o tempo de atividade do Windows. Ele não reinicia nem agenda nada e não garante cache frio por si só. Foi utilizado no complemento após reinício acima; controle estrito de cache não foi realizado nem é declarado validado.
