# Referência de desempenho — 3.0.7

**Correção na etapa 3.0.13:** o host antigo podia executar StartupUri ao processar o Dispatcher, abrindo uma segunda janela normal. Por isso, as tabelas anteriores de abertura/memória são históricas e não comprovam uma janela isolada; não usar suas diferenças como comparação de produção. O host foi corrigido e agora exige uma janela. A medição do motor de mídia é separada desse host, mas continua sujeita às limitações de carga/cache/amostra descritas abaixo.

Nova referência de host isolado em [performance/startup-isolated-host-3.0.13.json](performance/startup-isolated-host-3.0.13.json), medida antes do incremento da versão (número 3.0.12 no JSON, com as correções desta etapa): seis processos, uma janela por amostra, tema escuro, 144 DPI, runtime direto do apphost .NET 10.0.3. Mediana das cinco aberturas seguintes: **973 ms**; memória residente média: **121,0 MB**. O runtime anterior registrado era 10.0.14, e o isolamento mudou; não comparar numericamente para atribuir ganho/regressão. Não mede abertura fria, instalação self-contained ou memória da árvore FFmpeg.

Medição em 02/11/2026, antes da reorganização interna. Código de produção correspondente ao commit `234bf89`; a documentação foi atualizada em `6dfde05`. O benchmark foi adicionado em `tests/HeliumVideoTools.Tests/PerformanceBenchmarks.cs` sem alterar o aplicativo.

## Ambiente

- AMD Ryzen 7 3700X, 8 núcleos/16 processadores lógicos; 34.281.041.920 bytes de RAM reconhecidos pelo sistema (~31,93 GiB).
- Windows build 26300, x64; runtime do processo de teste .NET 10.0.14.
- FFmpeg/ffprobe 9.0.2 Gyan Essentials, binários do projeto. Hashes completos e saída de versão em [performance/baseline-3.0.7.json](performance/baseline-3.0.7.json).
- Compilação Release, host de testes dependente do runtime. Não é o executável instalado self-contained/ReadyToRun.
- Outras atividades normais do computador não foram suspensas. Não houve controle de cache de disco, energia, temperatura ou carga externa.

## Abertura e memória

O método inicial pretendia iniciar um processo novo apenas com `MainWindow(preview:true)`, medindo um callback ApplicationIdle após ContentRendered. A janela preview não lê/grava preferências, mas o StartupUri do App ainda podia iniciar uma segunda janela normal; a exclusão de preferências/consulta/recuperação não estava garantida pelo host. Ver a correção acima.

| Medida | Resultado |
| --- | --- |
| Primeira abertura observada | 1.116 ms |
| Cinco aberturas seguintes | 773–827 ms; mediana 780 ms |
| Memória residente do host no ponto de medição | 130,9–131,2 MB decimais |
| Memória privada do host no ponto de medição | 142,5–145,5 MB decimais |

Esses números não comprovam abertura fria: o sistema pode ter cache mesmo na primeira amostra. Também não medem o tempo de abertura da instalação real nem a memória do FFmpeg. Servem para comparar a mesma janela e o mesmo host antes/depois do refactor. A medição do aplicativo distribuído, abertura fria controlada e memória da árvore completa permanecem pendentes.

## Processamento

Entrada sintética: 45 segundos, 1280×720, 25 fps, H.264 com áudio AAC, 62.535.634 bytes. Gerada com `testsrc2` + `sine`, x264 ultrafast CRF 12. Dois ensaios sequenciais por cenário, usando o mesmo arquivo. SHA-256 do original conferido ao final.

| Cenário | Ensaio 1 | Ensaio 2 | Média | Saída |
| --- | ---: | ---: | ---: | ---: |
| Compactar H.264, manter áudio, duas etapas, alvo 65% do original (~40,65 MB) | 5,18 s | 5,02 s | 5,10 s | 38,62 MB |
| Dividir em 3 partes precisas, H.264/AAC | 3,72 s | 3,79 s | 3,76 s | 20,86 MB somados |
| WebM VP8/Opus, duas etapas, teto 299 MB limitado pelo orçamento abaixo do original | 15,97 s | 16,63 s | 16,30 s | 46,57 MB |

O tempo inclui preflight/ffprobe, codificação, validação interna do resultado, publicação e limpeza do motor. Exclui geração da entrada e uma sondagem adicional de verificação feita após cada ensaio. As saídas de compactação e WebM respeitaram o teto efetivamente aplicado. A divisão por quantidade não impõe teto de bytes. Os presets/taxas completos estão no JSON.

Não extrapolar estes tempos para gravações extensas de audiências. Conteúdo sintético, resolução, codecs, hardware e parâmetros alteram o custo. Dois ensaios são uma referência inicial, não uma distribuição estatística robusta. Não houve comparação Python/.NET nem uso de GPU.

## Repetir antes/depois

## Repetição solicitada — 02/11/2026

O usuário informou que havia programas pesados abertos na primeira medição e pediu a repetição. O mesmo comando foi executado sem modificar/fechar outros aplicativos. A carga externa não foi monitorada; não se afirma que o computador estivesse ocioso. O hash da entrada e os hashes dos dois executáveis coincidem com a primeira medição, com o mesmo runtime .NET 10.0.14 e os mesmos parâmetros. Registro bruto: [performance/retest-3.0.7.json](performance/retest-3.0.7.json).

| Medida | Primeira medição | Repetição |
| --- | ---: | ---: |
| Primeira abertura observada | 1.116 ms | 1.013 ms |
| Mediana das cinco aberturas seguintes | 780 ms | 796 ms |
| Memória residente média do host nas seis aberturas | 131,1 MB | 133,7 MB |
| Compactar H.264, média de dois ensaios | 5,10 s | 4,89 s |
| Dividir em três partes, média de dois ensaios | 3,76 s | 3,41 s |
| VP8/Opus, média de dois ensaios | 16,30 s | 14,33 s |

A repetição será a referência preferencial antes da reorganização; a primeira permanece como histórico. O processamento ficou cerca de 4,2%, 9,3% e 12,1% mais rápido, respectivamente. Isso é variação entre condições de teste, não uma otimização do código nem prova isolada de causalidade dos outros programas. A abertura repetida e memória variaram ligeiramente no sentido contrário. Mesmas limitações de cache, host isolado e amostra pequena; saídas verificadas e original preservado.

## Como repetir antes/depois

## Após separar Core/Infrastructure — 3.0.8

Executado em 02/11/2026 com a mesma máquina, runtime .NET 10.0.14, mesmos parâmetros e hashes de entrada/FFmpeg/ffprobe. Registro bruto: [performance/after-refactor-3.0.8.json](performance/after-refactor-3.0.8.json). A alteração de produção foi estrutural; não houve ajuste dos codecs ou comandos.

| Medida | Referência repetida 3.0.7 | Após separação 3.0.8 |
| --- | ---: | ---: |
| Mediana das cinco aberturas seguintes | 796 ms | 778 ms |
| Memória residente média do host nas seis aberturas | 133,7 MB | 133,7 MB |
| H.264, média de dois ensaios | 4,89 s | 4,78 s |
| Divisão, média de dois ensaios | 3,41 s | 3,51 s |
| VP8, média de dois ensaios | 14,33 s | 14,70 s |

Diferenças pequenas, em sentidos variados (~−2,2% a +2,9% no processamento), sem evidência de alteração material neste ensaio. Não são prova estatística de equivalência, ganho ou regressão; carga externa/cache continuam sem controle e há apenas duas amostras por operação. Não há motivo observado para otimização de codec nesta etapa. Saídas verificadas e original preservado.

## Reproduzir medições

## Após extrair configuração/resultados da janela — 3.0.9

Mesma entrada e mesmos hashes de FFmpeg/ffprobe da 3.0.8, .NET 10.0.14, parâmetros preservados e código de janela isolado. Registro em [performance/operation-editor-3.0.9.json](performance/operation-editor-3.0.9.json).

| Medida | 3.0.8 | 3.0.9 |
| --- | ---: | ---: |
| Mediana das cinco aberturas seguintes | 778 ms | 802 ms |
| Memória residente média do host | 133,7 MB | 133,9 MB |
| H.264, média de dois ensaios | 4,78 s | 4,73 s |
| Divisão, média de dois ensaios | 3,51 s | 3,44 s |
| VP8, média de dois ensaios | 14,70 s | 14,35 s |

Variações pequenas em sentidos diferentes, com mesmas limitações da metodologia (carga externa/cache não controlados e amostra pequena). Nenhuma alteração material observada; não se afirma equivalência estatística ou aceleração causada pelo refactor. Saídas verificadas e original preservado.

## Reproduzir

Com FFmpeg e ffprobe preparados e o SDK .NET 10 disponível:

```powershell
.\work\tools\dotnet\dotnet.exe run --project tests/HeliumVideoTools.Tests -c Release -- --benchmark work/performance-baseline ffmpeg.exe ffprobe.exe
```

O comando cria uma subpasta `run-<id>` nova e mantém amostras/saídas/JSON em `work`, sem sobrescrever vídeos do usuário. Gera seis aberturas e dois ensaios das três operações. Não faz parte de cada execução de testes ou build: repetir após mudanças de arquitetura/desempenho que justifiquem a comparação.

Antes de comparar, conferir mesma máquina, runtime, hashes dos executáveis, parâmetros, características/hash da entrada e carga externa semelhante. Não exigir igualdade de duração: comparar variação e repetir apenas se um desvio material precisar de investigação. Nenhuma otimização de produção foi aplicada com base nesta única medição.
