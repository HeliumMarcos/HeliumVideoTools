# Progresso acessível e isolamento dos testes — 3.0.13

Não há mudança visual nesta entrega. A comparação da janela principal anterior e nova, usando o mesmo host isolado e antes de alterar a versão, produziu sete PNGs idênticos por SHA-256 (temas claro/escuro/contrastes e Atualizações).

## Eventos para leitores de tela

LiveSetting Polite já existia no progresso, mas faltava emitir LiveRegionChanged. ProgressAnnouncer agora emite o evento após atualizar o texto, sem mover foco ou alterar o conteúdo visível. O primeiro progresso é elegível imediatamente; os seguintes têm intervalo mínimo de cinco segundos e textos iguais são suprimidos. Resultado, erro e cancelamento podem passar pelo intervalo, mas continuam Polite: o leitor decide quando falar. O início de cada tentativa reinicia a política.

Erro de validação antes de mostrar o painel e cancelamento anterior ao lote usam o status visível no rodapé. Erros de transporte UIA conhecidos são registrados, evitando que um cliente desconectado interrompa o processamento. Não se cria uma fila própria de falas.

Teste determinístico cobre 1000 atualizações, fronteira de cinco segundos, repetição, resultado e reinício. Outro teste abre a janela WPF e inicia um cliente MTA em processo separado, restrito ao HWND dessa janela: recebe primeiro progresso, progresso após o intervalo, resumos reais de sucesso/erro/cancelamento e validação com o painel oculto. Uma rajada de 100 atualizações e um resumo repetido não geram eventos adicionais. A propriedade Polite é verificada no provedor; o cliente confirma o transporte dos eventos e os nomes recebidos. Isso não é uma escuta de áudio do Narrador.

Referência primária: [Microsoft — regiões dinâmicas no WPF](https://learn.microsoft.com/accessibility-tools-docs/items/wpf/text_livesetting).

## Teclado e monitores

O teste percorre o mecanismo de navegação WPF Tab/Shift+Tab nas três operações, verificando ação principal, tamanho/unidade, nomes acessíveis e retorno aos campos. No estado de processamento, alcança Cancelar e ignora campos/ação desabilitados. Não é teste de teclas físicas nem substitui uso com tecnologia assistiva.

Uma janela própria foi movida pelos monitores disponíveis, sem ativar outras aplicações nem mudar escala/resolução. Ambiente local: um monitor, área útil de 3840×2088 pixels, 144 DPI (150%); limites respeitados. [Registro](accessibility/displays-3.0.13.json). O evento de DPI observado nessa janela não valida troca entre monitores com DPI diferente: não existe esse par no ambiente. Campanha física em 100/125/200% e Narrador continuam pendentes.

## Correção do host de testes

O teste de teclado identificou que os controles percorridos pertenciam a uma segunda janela: o callback de StartupUri podia executar a abertura normal durante o processamento do Dispatcher dos testes. Isso podia adquirir mutex, iniciar recuperação/consulta e afetar foco, recursos e medições.

App agora tem um modo interno isolado para os testes: carrega recursos sem abertura normal, mutex, recuperação ou consulta automática. Todos os hosts WPF usam esse modo. A abertura produtiva cria explicitamente a mesma MainWindow; a distribuição precisa passar pelo teste real de instalação/abertura/reinstalação. O teste de teclado e o benchmark exigem exatamente uma janela.

As antigas amostras de abertura/memória não devem mais ser descritas como referência garantidamente isolada. Mantidas como histórico, com correção em DESEMPENHO.md. Uma nova série confirma uma janela em cada amostra; condições e runtime diferentes impedem tratar diferenças como ganho/regressão. O processamento de mídia não foi alterado por esta entrega.

## Distribuição validada

A [execução 41487771927](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41487771927) passou na suíte, incluindo teclado/UIA/monitores disponíveis, processamento, recuperação, temas e instalação/reinstalação/interrupção do instalador. Isso valida também a abertura produtiva explícita da janela após retirar StartupUri. A [3.0.13 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.13) está publicada.

A [verificação 41488625824](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41488625824) passou com download público anônimo pelo cliente real: 172937554 bytes, SHA-256/MZ e atualização desde versões anteriores testadas, incluindo 3.0.12, sem oferta para a própria 3.0.13. Nenhuma cópia nova foi colocada na pasta Music. Narração de áudio e campanha física em outras escalas continuam pendentes.
