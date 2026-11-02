# Separação do núcleo e infraestrutura — plano de execução

Escopo aprovado: item 6, sem mudanças visuais ou de comportamento. Preservar comandos FFmpeg, codecs, limites, caminhos de preferências, identidade do app, cancelamento, recuperação, relatórios e atualização. Não alterar esquemas JSON nem conteúdo dos relatórios.

1. Estabelecer teste de fronteira: modelos/regras em Core, serviços em Infrastructure, sem dependência do núcleo na interface ou infraestrutura.
2. Separar modelos/opções de VideoEngine, cálculo de bitrate e modelo de preferências das implementações de disco/registro. Transferir execução, sondagem, motor, atualizações, recuperação, logs e relatórios para Infrastructure, que referencia Core. Preservar assinaturas usadas pelo aplicativo; namespaces dos serviços passam para Infrastructure.
3. Extrair execução de processo e leitura ffprobe de VideoEngine para serviços concretos de infraestrutura. Usar delegação nos métodos existentes, mantendo comandos, timeouts e erros.
4. Atualizar referências da interface e testes. Compilar e executar a suíte existente (mídia, recuperação, atualizações, preferências, temas e fechamento).
5. Repetir o mesmo benchmark da referência solicitada pelo usuário (retest-3.0.7.json), registrar diferenças/limitações sem atribuir variação normal a otimização.
6. Validar o instalador no GitHub e publicar versão oficial se as verificações passarem.

Esta entrega separa a primeira fronteira de responsabilidades. Reduzir o código da janela e demais ajustes de composição continua em etapa posterior, sem mudar o layout aprovado.

## Estrutura resultante

- `Core`: `VideoModels`, `BatchModels`, `VideoBitrate`, `VideoSizing`/`VideoEstimates`, `UserPreferences` e `StartupDefaults`.
- `Infrastructure`: `MediaProcess`, `MediaProbe`, `VideoEngine`, `BatchRunner`/`BatchReportStore`, `VideoEstimateService`, atualizações, recuperação, preferências de disco/registro e logs.
- `Desktop`: composição dos serviços, ViewModel, apresentação e interação WPF. Referencia Core e Infrastructure.
- `UpdateHost`: auxiliar de instalação independente, preservado.

Dependências: Desktop → Infrastructure → Core, com Desktop também consumindo contratos Core. Core não referencia Infrastructure ou Desktop. `VideoEngine.ExecuteAsync`, `ProbeAsync` e `Bitrate` continuam como métodos de entrada delegados; a fórmula de bitrate agora pertence a Core e não obriga as estimativas a dependerem do executável de processamento.

Namespaces de serviços mudaram de Core para Infrastructure, incluindo importações no app e testes. Não há migração dos dados do usuário: esquemas JSON não têm nomes de tipos/assemblies, e caminhos foram preservados. Nenhum pacote NuGet novo. O DLL da infraestrutura é incluído automaticamente pela referência do projeto e conferido no teste de instalação.

Verificação local: teste de fronteira reproduziu falha antes da separação e passou depois. Suíte de mídia, recuperação, atualizações, preferências, temas e fechamento passou. Comparação textual confirmou corpos de execução, sondagem e processamento preservados (somente delegação/namespace na sondagem). Compilação Release sem avisos/erros. Benchmark em DESEMPENHO.md.
