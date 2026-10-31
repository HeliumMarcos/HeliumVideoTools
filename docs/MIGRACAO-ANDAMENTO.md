# Migração .NET — primeira etapa

Estado em 30/10/2026: implementação C#/.NET 10 WPF incorporada à main; repositório público por decisão do usuário. A [3.0.2 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.2) está publicada com instalador, runtime, ferramentas, lotes tolerantes a falhas e resumo final aprovado. As pendências de validação continuam registradas abaixo.

Implementado:

- Projetos C#/.NET 10 para motor e interface WPF, sem dependências NuGet externas.
- Sondagem ffprobe JSON; execução assíncrona de FFmpeg, cancelamento da árvore do processo e logs rotativos locais.
- Compactação H.264/H.265, WebM VP8/VP9 e divisão; modo em duas etapas para codificação.
- MB decimais, orçamento com margem e validação do tamanho real antes de publicar a saída.
- WebM usa o menor orçamento entre o limite e 98% do tamanho original. Um MP4 menor não é expandido para ocupar 299 MB.
- Arquivos temporários próprios, publicação sem sobrescrever, originais preservados.
- Interface e marca aprovadas: seleção múltipla, arrastar/soltar, operações diretas sem fila, progresso geral, cancelamento e Abrir pasta.
- Janela Atualizações aprovada e integrada: consulta automática assíncrona de versões estáveis, download anônimo com hash, instalador por usuário e atualização conjunta FFmpeg/ffprobe com restauração.
- GitHub Actions atualizado: checkout v7.0.1, setup-dotnet v6.0.0 e upload-artifact v7.0.2 usam Node.js 24. A execução 41133957251 passou pelos testes, instalação, abertura, reinstalação com app aberto, mesma pasta e preservação dos bytes do atalho.
- Conjunto do projeto atualizado de FFmpeg/ffprobe 8.1.2 para 9.0.2, mesma distribuição Gyan Essentials, ZIP conferido por SHA-256 e codecs necessários verificados. Script de preparação guarda backup e tenta restaurar o conjunto se a cópia falhar.

Validação: compilação Release sem avisos/erros; testes com mídia artificial incluindo áudio, compactação em duas etapas, VP8/VP9, limite sem aumento sobre original, divisão por quantidade, progresso, cancelamento e limpeza de temporários. Isso não atesta aceitação no Projudi/SEEU nem valida ainda a continuidade audiovisual de uma audiência dividida.

## Aprovação visual

O usuário aprovou o visual V2 das três ferramentas e a identidade A (Vídeo em movimento), já aplicada e registrada em `docs/brand-guidelines.md`. Novas telas devem ser apresentadas por comparação antes/depois antes de ativar na versão final.

Também aprovou a comparação da janela Atualizações compacta (720 × 624 DIPs) e da barra de título escura nativa. Testes locais verificaram ausência de rolagem no estado de download/verificação e aceitação das cores pelo DWM no Windows 11. O título preserva os controles nativos; não foi criada uma moldura personalizada.

## Distribuição validada

A [execução 41135780937](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41135780937) concluiu com sucesso: processamento, instalação, abertura WPF, reinstalação com app aberto, reabertura automática via `/RESTART`, mesma pasta e preservação dos bytes do atalho. A fixação real no Windows do usuário e o fluxo completo entre duas versões estáveis ainda precisam de validação. A release inclui instalador e `SHA256SUMS.txt`.

A [execução 41137905761](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41137905761) repetiu esses testes e publicou a 3.0.1 no canal oficial. Após publicar, o mesmo cliente do aplicativo baixou anonimamente o instalador (172743302 bytes), conferiu SHA-256/MZ e consultou o endpoint real de versão estável: detectou atualização de 3.0.0 para 3.0.1 e não ofereceu atualização para a própria 3.0.1. Isso não substitui o teste visual da notificação e da instalação pelo usuário na instalação anterior.

O usuário confirmou que a atualização pelo aplicativo funcionou no seu computador. Isso valida o fluxo relatado, mas não confirma por si só a preservação de atalhos fixados.

## Etapa 3.0.2 — lotes e continuidade

Publicação concluída pela [execução 41153532453](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41153532453), após testes de processamento, instalação, reinstalação e reabertura. O download público real e a consulta de atualização de versões anteriores são conferidos pelo cliente do próprio aplicativo.

- Resumo final aprovado pelo usuário: sem fila; mostra concluídos/erros/cancelados/não iniciados, relatório e escolha da pasta quando há várias.
- `BatchRunner` independente da interface continua após falha de arquivo, distingue timeout de cancelamento solicitado, preserva partes já publicadas e não deixa callbacks atrasados regredirem o progresso.
- ViewModel integrado ao novo motor; relatórios JSON e TXT locais em `%LOCALAPPDATA%\HeliumVideoTools\reports`, gravados por temporários próprios. O relatório contém caminhos locais; não é enviado ao GitHub.
- Teste de regressão detectou sobreposição no corte por cópia de fluxo: fonte de 200 quadros resultou em 300 ao juntar partes. As três divisões agora usam busca precisa e recodificação H.264/AAC em MP4, preservando a cadência com `fps_mode passthrough`. Isso pode demorar mais que copiar o fluxo, mas não depende de coincidir com um quadro-chave. Original preservado.
- Testes passam com quadros marcados CFR e VFR: contagem e ordem visual preservadas em quantidade, duração e tamanho; áudio decodificável, soma de duração e diferença de duração A/V dentro das tolerâncias explícitas do teste. Isso ainda não comprova continuidade perceptual/sincronismo em audiências extensas ou todos os formatos.
- Referência técnica do comportamento de busca/cópia de fluxo: https://www.ffmpeg.org/ffmpeg.html (opção `-ss`).

## Pendências do plano após a publicação oficial

Etapa 3.0.3 preparada após aprovação visual: estimativas usam metadados em segundo plano com cache, separam teto/orçamento/referência e sinalizam mídias sem orçamento válido. Preferências úteis são salvas atomicamente em `%LOCALAPPDATA%\HeliumVideoTools\preferences.json`, fora da instalação: última pasta, dimensões/maximização, quantidade/duração de divisão e modo em duas etapas. Migração inicial lê apenas opções compatíveis do QSettings antigo, sem alterar o registro de origem. Geometria binária Qt, tema ainda não implementado e padrões protegidos não são importados.

Padrões protegidos: iniciar em Compactar, Personalizada/98 MB, áudio original ligado e WebM VP8/299 MB. Alvo, unidade, codec, preset e página inicial não são gravados nas preferências. JSON inválido não bloqueia a abertura; é preservado como `.invalid-<id>` na próxima gravação, e os padrões são usados. Testes de renderização usam modo isolado, sem ler/gravar preferências reais.

- Completar validação de DPI, tema claro/alto contraste e teclado; visual escuro e ícone já aprovados.
- Preferências úteis e importação compatível implementadas na etapa 3.0.3; padrões de abertura protegidos. Ampliar cobertura de monitores/DPI e opções quando novos temas forem implementados.
- Divisão precisa e testes sintéticos CFR/VFR implementados. Ainda falta validar continuidade perceptual, sincronismo e qualidade com amostras extensas reais.
- Codificação acima do limite agora repete com orçamento reduzido, até três tentativas; falha final não publica arquivo excedente.
- Recuperação de resultados parciais e erros por arquivo implementada na etapa 3.0.2. Continuar ampliando casos de erro e recuperação de encerramento inesperado.
- Atualização pública sem credenciais e interação integradas, com confirmação de funcionamento pelo usuário. Repetir entre versões oficiais e em falhas/interrupções, sem tratar isso como bloqueio da migração já concluída.
- Ferramentas em conjuntos imutáveis, validação, troca atômica de ponteiro e restauração integradas; falta validar transação completa em perfil isolado.
- Instalador NSIS por usuário validado em runner para reinstalação real, pasta fixa e bytes dos atalhos; fixação real continua dependente de validação no Windows do usuário.
- Publicar no canal oficial, conforme solicitação do usuário, somente após testes de distribuição; continuar registrando as limitações sem tratá-las como validações concluídas.

## Compilar/testar

Com SDK .NET 10:

```powershell
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project tests/HeliumVideoTools.Tests -c Release
.\scripts\Prepare-MediaTools.ps1
```

O SDK local de desenvolvimento foi extraído em `work/tools/dotnet` após conferir SHA-512 contra os metadados oficiais da Microsoft. `work` e binários não são enviados ao Git.

O manifesto fixado em `tools/manifest.json` prepara o conjunto do instalador. Ferramentas posteriormente atualizadas ficam no perfil fora da instalação, para não serem rebaixadas pelo instalador. O cliente de Releases usa acesso público anônimo, sem dependência de GitHub CLI ou tokens.
