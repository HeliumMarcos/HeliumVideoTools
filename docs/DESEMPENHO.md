# Referência de desempenho — 3.0.7

Medição em 02/11/2026, antes da reorganização interna. Código de produção correspondente ao commit `234bf89`; a documentação foi atualizada em `6dfde05`. O benchmark foi adicionado em `tests/HeliumVideoTools.Tests/PerformanceBenchmarks.cs` sem alterar o aplicativo.

## Ambiente

- AMD Ryzen 7 3700X, 8 núcleos/16 processadores lógicos; 34.281.041.920 bytes de RAM reconhecidos pelo sistema (~31,93 GiB).
- Windows build 26300, x64; runtime do processo de teste .NET 10.0.14.
- FFmpeg/ffprobe 9.0.2 Gyan Essentials, binários do projeto. Hashes completos e saída de versão em [performance/baseline-3.0.7.json](performance/baseline-3.0.7.json).
- Compilação Release, host de testes dependente do runtime. Não é o executável instalado self-contained/ReadyToRun.
- Outras atividades normais do computador não foram suspensas. Não houve controle de cache de disco, energia, temperatura ou carga externa.

## Abertura e memória

Cada amostra inicia um processo novo com a janela real `MainWindow(preview:true)`. O ponto final é um callback do dispatcher em ApplicationIdle agendado após ContentRendered. O teste não lê/grava preferências reais e não executa atualização automática ou recuperação na abertura.

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

Com FFmpeg e ffprobe preparados e o SDK .NET 10 disponível:

```powershell
.\work\tools\dotnet\dotnet.exe run --project tests/HeliumVideoTools.Tests -c Release -- --benchmark work/performance-baseline ffmpeg.exe ffprobe.exe
```

O comando cria uma subpasta `run-<id>` nova e mantém amostras/saídas/JSON em `work`, sem sobrescrever vídeos do usuário. Gera seis aberturas e dois ensaios das três operações. Não faz parte de cada execução de testes ou build: repetir após mudanças de arquitetura/desempenho que justifiquem a comparação.

Antes de comparar, conferir mesma máquina, runtime, hashes dos executáveis, parâmetros, características/hash da entrada e carga externa semelhante. Não exigir igualdade de duração: comparar variação e repetir apenas se um desvio material precisar de investigação. Nenhuma otimização de produção foi aplicada com base nesta única medição.
