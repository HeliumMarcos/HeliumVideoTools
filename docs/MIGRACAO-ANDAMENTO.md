# Migração .NET — primeira etapa

Escopo atual: por decisão do usuário em 01/11/2026, a continuação fica restrita aos itens **2 (recuperação), 3 (telas/acessibilidade), 5 (desempenho) e 6 (organização interna)**. O [escopo restante](ESCOPO-RESTANTE.md) define a sequência e os critérios. Audiências reais/aceitação nos sistemas, teste manual de pins e assinatura digital não fazem parte desta continuação; menções históricas abaixo não os recolocam no escopo ativo.

Estado em 01/11/2026: implementação C#/.NET 10 WPF incorporada à main; repositório público por decisão do usuário. A [3.0.5 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.5) está publicada com instalador, runtime, ferramentas, lotes tolerantes a falhas, estimativas aprovadas, preferências úteis, recuperação do conjunto FFmpeg/ffprobe e temas aprovados. As pendências de validação continuam registradas abaixo.

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

Etapa 3.0.3 publicada após aprovação visual: estimativas usam metadados em segundo plano com cache, separam teto/orçamento/referência e sinalizam mídias sem orçamento válido. Preferências úteis são salvas atomicamente em `%LOCALAPPDATA%\HeliumVideoTools\preferences.json`, fora da instalação: última pasta, dimensões/maximização, quantidade/duração de divisão e modo em duas etapas. Migração inicial lê apenas opções compatíveis do QSettings antigo, sem alterar o registro de origem. Geometria binária Qt, tema ainda não implementado e padrões protegidos não são importados.

A [execução 41155944596](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41155944596) passou pelos testes de processamento, instalação, reinstalação/reabertura, atalhos e preservação de preferências. O download público real foi conferido (172810793 bytes, SHA-256 e MZ); o cliente detectou a release desde as versões anteriores testadas, incluindo 3.0.2, e não ofereceu atualização para si mesmo. Testes locais também conferiram cache/invalidação, cancelamento/timeout e estimativa WPF com ffprobe real nos tamanhos padrão e mínimo.

Padrões protegidos: iniciar em Compactar, Personalizada/98 MB, áudio original ligado e WebM VP8/299 MB. Alvo, unidade, codec, preset e página inicial não são gravados nas preferências. JSON inválido não bloqueia a abertura; é preservado como `.invalid-<id>` na próxima gravação, e os padrões são usados. Testes de renderização usam modo isolado, sem ler/gravar preferências reais.

- Temas claro/escuro/Sistema e alto contraste aprovados e integrados na etapa 3.0.5; testes de teclado e dimensões lógicas adicionados. Continuam pendentes leitor de tela, todas as paletas personalizadas e monitores com DPI diferente.
- Preferências úteis e importação compatível implementadas na etapa 3.0.3; padrões de abertura protegidos. Ampliar cobertura de monitores/DPI e opções quando novos temas forem implementados.
- Divisão precisa e testes sintéticos CFR/VFR implementados. Ainda falta validar continuidade perceptual, sincronismo e qualidade com amostras extensas reais.
- Codificação acima do limite agora repete com orçamento reduzido, até três tentativas; falha final não publica arquivo excedente.
- Recuperação de resultados parciais e erros por arquivo implementada na etapa 3.0.2. Continuar ampliando casos de erro e recuperação de encerramento inesperado.
- Atualização pública sem credenciais e interação integradas, com confirmação de funcionamento pelo usuário. Repetir entre versões oficiais e em falhas/interrupções, sem tratar isso como bloqueio da migração já concluída.
- Ferramentas em conjuntos imutáveis, validação, troca atômica de ponteiro e restauração integradas. A etapa 3.0.4 valida a transação em perfil isolado com executáveis reais e transporte simulado; resta testar queda de energia/encerramento abrupto e download real pela interface.
- Instalador NSIS por usuário validado em runner para reinstalação real, pasta fixa e bytes dos atalhos; fixação real continua dependente de validação no Windows do usuário.
- Publicar no canal oficial, conforme solicitação do usuário, somente após testes de distribuição; continuar registrando as limitações sem tratá-las como validações concluídas.

## Etapa 3.0.4 — recuperação do conjunto FFmpeg/ffprobe

Retorno à distribuição embutida na primeira atualização, reparo de active.json inválido sem bloquear uma instalação nova e preservação do ponto anterior. Operações usam uma captura única de FFmpeg/ffprobe, incluindo a validação dos hashes do conjunto capturado, para evitar misturar ferramentas durante a troca do ponteiro. Diretórios incompletos de tentativas que falham são removidos; conjuntos ativados são imutáveis e retidos para operações já iniciadas.

Testes isolados cobrem instalação real dos executáveis, retorno às ferramentas embutidas, conjunto anterior, pacote incompleto, SHA-256 incorreto, versão divergente, cancelamento no fim do download e antes da ativação/restauração, arquivo temporário interrompido, JSON corrompido, identidade com travessia de diretório e backup adulterado. Nenhum teste altera o perfil real do usuário. O transporte usa um HttpMessageHandler local; isso não atesta disponibilidade da origem pública, nem simula queda de energia. Recuperação de temporários após encerramento forçado do processamento continua pendente.

A [execução 41327680947](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41327680947) passou pelos testes de processamento e reinstalação/reabertura com preservação de preferências e atalhos, e publicou a 3.0.4 estável. Uma tentativa anterior bloqueou a publicação ao detectar que o fechamento não salvou preferências. O instalador agora envia uma mensagem registrada à janela principal, inclusive oculta/minimizada; o WPF agenda o fechamento final após retornar do evento Closing, evitando reentrância se a gravação terminar rapidamente. O prazo de encerramento forçado permanece como último recurso. Teste local adicional repetiu oito fechamentos normais/pela mensagem com preferências isoladas; o comando --verify-close foi incorporado ao workflow para as próximas execuções.

Download público anônimo real validado: instalador com 172818105 bytes, SHA-256 e assinatura de formato MZ conferidos pelo cliente do próprio aplicativo. O canal estável ofereceu atualização às versões anteriores testadas, incluindo 3.0.3, e não ofereceu à própria 3.0.4. Assinatura de formato MZ não é assinatura digital Authenticode.

## Etapa 3.0.5 — temas aprovados

Usuário aprovou a comparação antes/depois e autorizou integração. Recursos semânticos dinâmicos substituem cores fixas nas duas janelas, opções criadas em código, resultados e menu de pastas. Seletor Sistema/Claro/Escuro persiste nas preferências; alto contraste do Windows prevalece e usa cores pareadas de SystemColors. Assinatura e barra nativa seguem a aparência, mantendo controles e Snap do Windows.

Validação local: três ferramentas em dois tamanhos (1280×900 e 980×700 DIPs), duas paletas de contraste simuladas sem alterar o sistema, contraste de texto/ações, menus selecionados, nomes acessíveis, ordem de Tab, foco real de campo com borda de 2 DIPs e preservação dos valores na troca de tema. Atualizações permanece sem rolagem no tamanho padrão em consulta, download e verificação. Simulação de cores e renderização não atestam leitor de tela, monitores/DPI reais nem todas as personalizações possíveis do usuário.

A [execução 41329605153](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41329605153) repetiu os testes de processamento, fechamento e temas, instalou/reinstalou com o aplicativo aberto, conferiu tema salvo, preferências e bytes do atalho e publicou a 3.0.5 estável. A janela apareceu no runner em 0,81 s; isso não é medição de abertura fria no computador do usuário. O download público real foi validado pelo cliente do app: 172857972 bytes, SHA-256 e MZ; versões anteriores testadas, incluindo 3.0.4, detectaram a atualização, e a própria 3.0.5 não recebeu atualização para si mesma.

## Comandos

Com SDK .NET 10:

```powershell
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project tests/HeliumVideoTools.Tests -c Release
.\scripts\Prepare-MediaTools.ps1
```

O SDK local de desenvolvimento foi extraído em `work/tools/dotnet` após conferir SHA-512 contra os metadados oficiais da Microsoft. `work` e binários não são enviados ao Git.

O manifesto fixado em `tools/manifest.json` prepara o conjunto do instalador. Ferramentas posteriormente atualizadas ficam no perfil fora da instalação, para não serem rebaixadas pelo instalador. O cliente de Releases usa acesso público anônimo, sem dependência de GitHub CLI ou tokens.
