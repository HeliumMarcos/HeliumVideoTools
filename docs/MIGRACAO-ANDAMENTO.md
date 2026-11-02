# Migração .NET — primeira etapa

**Item 5 — instalação real medida em 02/11/2026:** 3.0.14 self-contained, abertura repetida mediana 727 ms e residente médio 136,8 MB. Fonte artificial de 180 s/720p (~250 MB) processada pela interface: H.264/98 MB 18,28 s, três partes 12,78 s e VP8 59,45 s. Memória simultânea do app/filhos e verificações de áudio/duração/teto/hash registradas em [DESEMPENHO-INSTALADO.md](DESEMPENHO-INSTALADO.md). Preferências restauradas byte a byte. Apenas testes e documentação adicionados; sem nova release ou mudança de produção. Abertura fria controlada ainda não medida; nenhum reinício/limpeza de cache foi provocado. Modo só de abertura preparado e verificado para observação futura após reinício escolhido pelo usuário.

**Escopo atualizado por decisão do usuário em 02/11/2026:** escuta com Narrador e testes físicos de escalas/monitores foram dispensados. Não continuam como tarefas pendentes e não devem ser solicitados novamente. Menções abaixo são histórico de validações não realizadas. Itens 2 e 3 encerrados no escopo acordado; segue o item 5 de desempenho. Não há alteração do aplicativo ou necessidade de nova release por esta decisão.

Escopo atual: por decisão do usuário em 01/11/2026, a continuação fica restrita aos itens **2 (recuperação), 3 (telas/acessibilidade), 5 (desempenho) e 6 (organização interna)**. O [escopo restante](ESCOPO-RESTANTE.md) define a sequência e os critérios. Audiências reais/aceitação nos sistemas, teste manual de pins e assinatura digital não fazem parte desta continuação; menções históricas abaixo não os recolocam no escopo ativo.

Estado em 02/11/2026: implementação C#/.NET 10 WPF incorporada à main; repositório público por decisão do usuário. A [3.0.14 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.14) está publicada, incluindo recuperação do histórico do lote e entrega conjunta dos itens 2/3 possíveis neste ambiente. O usuário deixou a escuta do Narrador pendente; testes físicos com outras escalas/monitores mistos continuam dependentes de ambiente. Não declarar essas validações concluídas. Distribuir pelo GitHub, sem cópia nova de instalador na pasta Music.

Entrega 3.0.14: registro atômico e exclusivo de lote, checkpoints de início/publicação/resultado, relatórios recuperados por ID e resumo existente restaurado ao abrir. Processamento/registro fora da thread WPF; sem fila ou retomada de codificação. Testes encerram worker real, preservam saídas, ignoram lote ativo/corrupção e repetem após falha de relatório. Recuperação tardia não substitui lote atual. Render do resumo em quatro escalas, teclado/UIA/temas/áreas menores e suite de mídia/recuperação anteriores passaram em [41493104918](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41493104918), junto com instalação/reinstalação/interrupção do instalador. [41493942250](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41493942250) confirmou download público sem token (172961238 bytes, hash/MZ) e atualização desde versões anteriores testadas incluindo 3.0.13, sem oferta para si mesma. Detalhes em BATCH-RECOVERY.md; limitações de persistência física e intervalo publicação/registro explicitadas.

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

## Etapa 3.0.6 — primeira entrega de recuperação após interrupções

`RecoveryJournal` registra os novos temporários de vídeo e estatísticas antes de criá-los, mantendo um arquivo exclusivo aberto durante a operação. Gravação e limpeza do registro/temporário são executadas fora da thread da interface. Ao abrir, a recuperação em segundo plano ignora registros bloqueados, limpa artefatos registrados de tarefas interrompidas e preserva saídas publicadas. Não faz busca por extensão nas pastas dos vídeos nem tenta identificar temporários de versões antigas.

Diretórios redirecionados são resolvidos ao registrar; a recuperação recusa reparse points posteriores. Pastas de estatísticas só são removidas quando contêm apenas os nomes conhecidos, sem links ou subpastas. Conteúdo desconhecido e registros inválidos permanecem para inspeção, sem excluir dados por suposição. A recuperação não retoma a codificação nem reconstrói o relatório de um lote interrompido.

Cada processo de FFmpeg/ffprobe iniciado pelo motor recebe um Job Object não herdável com `KILL_ON_JOB_CLOSE`. A associação é feita logo após `Process.Start`; existe uma janela curta antes da associação, que não foi eliminada por esta etapa. Falha de associação em processo ainda ativo aborta e tenta encerrá-lo. Não há enumeração/encerramento por nome de processos de outros aplicativos. Referência: https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects.

Teste isolado força somente o fechamento do hospedeiro enquanto o FFmpeg grava um MP4 real; confirma o encerramento do filho associado e a continuidade de um segundo FFmpeg independente. Verifica recuperação bloqueada durante tarefa ativa, limpeza após a morte do host, cancelamento/idempotência, JSON corrompido e preservação de originais, resultados e conteúdos não registrados/desconhecidos. Não simula desligamento real do computador. Interrupções de atualização das ferramentas/instalador e recuperação de histórico continuam pendentes no item 2; itens 3, 5 e 6 seguem no escopo aprovado.

A [execução 41371554496](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41371554496) passou pelos testes de mídia, recuperação real do processo, fechamento, temas e instalação/reinstalação com reabertura e publicou a 3.0.6 estável. Download anônimo real conferido pelo cliente do próprio app: 172872698 bytes, SHA-256 e MZ; versões anteriores testadas, incluindo 3.0.5, detectaram a atualização, e a 3.0.6 não recebeu atualização para si mesma.

## Etapa 3.0.7 — interrupções das atualizações

Transações do conjunto de ferramentas têm registro exclusivo por ID, recuperação na abertura, validação do ativo e retorno ao anterior/embutido quando necessário. Limpeza respeita conjuntos referenciados, conteúdos conhecidos e bloqueios. Testes encerram o worker durante download, após download, extração, backup e ativação, além de danificar ativo/ponteiro após ativação. O teste aguarda a liberação observável do registro após encerrar o processo; um registro ainda bloqueado permanece adiado. Limpeza de arquivos possui repetição limitada para bloqueios transitórios do Windows, sem alterar permissões.

Instalador restaura app.previous quando app está ausente ao ser executado novamente; conteúdo residual de app é preservado em app.interrupted. A compilação isolada de teste usa raiz e payload definidos na compilação e não escreve atalhos/registro do produto. Casos encerram o instalador após extração e renomeação, incluindo diretório residual, e verificam a reinstalação posterior.

A [execução 41379179432](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41379179432) passou em processamento, ferramentas interrompidas, fechamento, temas, instalação/reinstalação e interrupção do instalador em raízes isoladas, publicando a 3.0.7 estável. A preparação do NSIS agora confere a existência do compilador e repete instalação até três vezes após falhas transitórias do provedor. Sem alterações visuais. Não houve teste de desligamento físico/queda de energia; reconstrução do histórico do lote e janela curta entre iniciar/associar processos ao Job Object continuam como limitações registradas.

## Item 5 — referência antes do refactor

Medições iniciais de 02/11/2026 registradas em [DESEMPENHO.md](DESEMPENHO.md), incluindo método, limitações, ambiente, entrada e parâmetros. Janela real em host isolado: primeira amostra 1.116 ms; cinco seguintes 773–827 ms, mediana 780 ms; memória residente do host ~131 MB. Mídia sintética de 45 s/720p: média de dois ensaios de 5,10 s para H.264 em duas etapas, 3,76 s para três partes precisas e 16,30 s para VP8 em duas etapas. Saídas sondadas, teto aplicável e hash original conferidos.

O host não carrega preferências, consulta de atualização ou recuperação inicial e não representa a distribuição self-contained. Não foi medido cache frio controlado nem memória de toda a árvore FFmpeg. A referência não é alegação de ganho ou promessa para audiências. Próxima etapa: organização interna (item 6), mantendo comportamento e comparando o mesmo benchmark. Apenas testes/documentação foram adicionados; nenhuma release nova é necessária para esta medição.

## Etapa 3.0.8 — primeira fronteira de arquitetura

Core passa a conter modelos/contratos, preferências tipadas, padrões protegidos e regras de tamanho/bitrate. Infrastructure referencia Core e implementa motor, execução de processos, sondagem, cache de metadados, lotes/relatórios, preferências de disco/registro, logs, recuperação e atualizações. Desktop compõe os serviços sem mudança visual; UpdateHost permanece independente. Métodos de entrada do motor delegam às implementações extraídas. Esquemas JSON/caminhos permanecem, sem migração dos dados do usuário. A redução do código da janela continua pendente no item 6.

Teste de fronteira reproduziu a ausência da separação antes e passou depois. Suíte local de mídia, interrupções/recuperação, preferências, temas e fechamento passou; build Release sem avisos/erros. Comparação textual dos corpos de execução/sondagem/processamento confirmou preservação. Benchmark com mesma entrada/ferramentas: abertura mediana 778 ms, memória média do host 133,7 MB, H.264 4,78 s, divisão 3,51 s e VP8 14,70 s. Diferenças pequenas frente à repetição de referência, sem alegação estatística de equivalência/ganho; limitações em DESEMPENHO.md.

A [execução 41383735979](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41383735979) passou em testes funcionais/arquitetura, instalação/reinstalação com app aberto, presença de Infrastructure.dll no pacote, preferências e interrupção do instalador isolado, publicando a 3.0.8 estável. Plano e detalhes em REFACTOR-INFRASTRUCTURE.md.

Download público anônimo da 3.0.8 validado pelo cliente do app: 172903717 bytes, SHA-256 e formato MZ; versões anteriores testadas, incluindo 3.0.7, detectaram a atualização e a 3.0.8 não recebeu atualização para si mesma.

## Etapa 3.0.9 — editor de operações e apresentação de resultados

OperationEditor assume criação/leitura dos campos e preferências compatíveis; BatchResultPresentation assume textos, contagens e estados do resumo. MainWindow caiu de 334 para 235 linhas não vazias. Não houve mudança de XAML/estilos, parâmetros de vídeo ou formato das preferências. Sete imagens de temas tiveram SHA-256 idêntico antes/depois da extração, antes do incremento da versão.

A [execução 41385183429](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41385183429) passou nos testes de mídia, arquitetura, recuperação, fechamento, temas, editor, instalação/reinstalação e interrupção do instalador, publicando a versão estável. Download anônimo pelo cliente real validou 172908312 bytes, SHA-256 e formato MZ; versões anteriores testadas, incluindo 3.0.8, detectaram atualização e a 3.0.9 não recebeu atualização para si mesma.

Benchmark controlado: abertura repetida mediana 802 ms, memória média do host 133,9 MB, H.264 4,73 s, divisão 3,44 s e VP8 14,35 s. Não representa abertura fria instalada nem comprova ganho estatístico. Coordenação de atualização, estimativas e encerramento permanece como continuação do item 6; telas/DPI/acessibilidade seguem depois, com prévia para qualquer alteração visual. Evidências em REFACTOR-WINDOW.md e DESEMPENHO.md.

## Etapa 3.0.10 — coordenação das estimativas

EstimateCoordinator passa a coordenar espera, cancelamento, substituição das solicitações e reutilização do serviço de metadados. A janela aplica a apresentação sem mudanças de texto/estilo. Testes controlados cobrem respostas e erros antigos, análise substituída, espera cancelada, limpeza/navegação e encerramento. Consulta real com FFprobe passou em 1280×900 e 980×700 e integra o workflow. Sete imagens de temas antes/depois foram idênticas por hash, antes do incremento da versão.

A [execução 41389325642](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41389325642) passou em processamento, recuperação, fechamento, temas, editor, estimativas WPF e instalação/reinstalação/interrupção do instalador. Download público da 3.0.10 validado pelo cliente real: 172910552 bytes, SHA-256 e MZ; atualização detectada desde versões anteriores testadas, incluindo 3.0.9, e ausente para a própria 3.0.10. Atualização e encerramento ainda estão na janela; item 6 não está inteiramente concluído. Nenhuma alegação nova de desempenho nesta entrega.

## Etapa 3.0.11 — atualização e encerramento

Consulta inicial após recuperação/uma vez, janela de Atualizações e encerramento passam a coordenadores próprios. Callbacks de rede após fechamento não publicam resultado; repetição de fechamento não repete cancelamento/gravação; o lote termina antes do salvamento e fechamento agendado. Reabertura imediata de Atualizações passa a descartar a janela fechada. Captura de geometria e apresentação permanecem no WPF. Nenhum XAML/estilo ou comando de vídeo alterado.

Suíte local e [execução 41390394652](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41390394652) passaram, incluindo oito fechamentos WPF, controle da janela Atualizações, testes de ciclo de vida, temas, processamento, recuperação e instalação/reinstalação/interrupção. Sete imagens antes/depois idênticas com teste estabilizado aguardando a transição habilitado/desabilitado. A tentativa de verificação pública local foi limitada pela cota anônima HTTP 403; a [execução independente 41390951856](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41390951856) confirmou download sem token pelo cliente real, 172917156 bytes, hash/MZ e atualização desde versões anteriores testadas, incluindo 3.0.10, sem oferta à própria 3.0.11.

As extrações da janela previstas nesta sequência estão implementadas. Seguem telas/DPI/acessibilidade com prévia antes de mudanças visuais; continuam as limitações de recuperação e de desempenho descritas no escopo. Não se declara todo o plano concluído.

## Comandos

Etapa 3.0.13: eventos acessíveis de progresso/resultado, intervalo de cinco segundos para periódicos, textos repetidos suprimidos e validação com painel oculto anunciada pelo rodapé. Cliente UIA separado e navegação WPF Tab/Shift+Tab passaram. Um monitor real em 150% verificado; áudio do Narrador e monitores mistos permanecem pendentes. Sete imagens antes/depois idênticas. O teste detectou abertura automática de uma segunda janela no host antigo; agora os testes usam App isolado, e a abertura produtiva cria explicitamente a mesma janela. Referências antigas de abertura/memória ficam como histórico; nova amostra confirma uma janela, com runtime/DPI registrados, sem alegação de ganho. Ver ACCESSIBLE-PROGRESS.md e DESEMPENHO.md.

[Execução 41487771927](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41487771927) passou na suíte e distribuição da 3.0.13. [Verificação 41488625824](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41488625824) confirmou download público anônimo real (172937554 bytes, SHA-256/MZ) e atualização desde versões anteriores testadas incluindo 3.0.12, sem oferta à própria versão. Sem cópia nova na pasta Music.

Etapa 3.0.12: adaptação de janela/lateral/formulário/ação aprovada após comparação antes/depois. ResponsiveLayout preserva campos/valores, restaura layout amplo e permite rolagem da ação em áreas muito baixas. WindowWorkArea ajusta a janela ao monitor sem ativá-la; manifesto PerMonitorV2 mantém asInvoker. Botões de arquivos/barra geral recebem nomes acessíveis. Preferências aceitam dimensões compactas de 675×329 e rejeitam números inválidos. [Execução 41466735914](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41466735914) passou na suíte de processamento, recuperação, preferências, temas, responsividade, fechamento e distribuição.

[Verificação 41468872516](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41468872516) confirmou download público anônimo real (172935077 bytes, SHA-256/MZ), atualização desde versões anteriores testadas incluindo 3.0.11 e ausência de atualização para a própria versão. Não houve nova cópia na pasta Music. Permanecem pendentes DPI físico/monitores mistos/Narrador; documentação e prévias em RESPONSIVE-ACCESSIBILITY.md.

Download anônimo real da 3.0.7 validado pelo cliente do aplicativo: 172888414 bytes, SHA-256 e formato MZ. Versões anteriores testadas, incluindo 3.0.6, detectaram a atualização; a 3.0.7 não recebeu atualização para si mesma.

Com SDK .NET 10:

```powershell
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project tests/HeliumVideoTools.Tests -c Release
.\scripts\Prepare-MediaTools.ps1
```

O SDK local de desenvolvimento foi extraído em `work/tools/dotnet` após conferir SHA-512 contra os metadados oficiais da Microsoft. `work` e binários não são enviados ao Git.

O manifesto fixado em `tools/manifest.json` prepara o conjunto do instalador. Ferramentas posteriormente atualizadas ficam no perfil fora da instalação, para não serem rebaixadas pelo instalador. O cliente de Releases usa acesso público anônimo, sem dependência de GitHub CLI ou tokens.
