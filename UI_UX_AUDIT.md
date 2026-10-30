# Auditoria UI/UX — Helium Vídeo Tools 2.2

## Problemas observados nas capturas da versão 2.0

- O símbolo antigo comunicava apenas reprodução de vídeo e não tinha relação clara com “Helium”.
- As listas vazias ocupavam muita altura sem explicar onde soltar os arquivos.
- Os botões ficavam visualmente sobre a área pontilhada, enfraquecendo a separação entre seleção e conteúdo.
- Campos e combos se esticavam por toda a janela, aumentando o percurso visual e dificultando a leitura.
- Os combos não exibiam um chevron claro; o controle de quantidade usava setas pequenas e pouco acessíveis.
- O campo “Tamanho final” continuava visível quando um preset automático estava selecionado, sugerindo que ainda poderia ser editado.
- A fila vazia era apenas uma grande região sem feedback ou próxima ação.
- O indicador de FFmpeg parecia texto neutro, embora representasse um estado de sucesso.
- A ação “Dispensar” de uma tarefa com erro ficava desabilitada.
- Em lotes, cada arquivo concluído emitia um som, causando feedback repetitivo.
- Algumas escolhas não eram persistidas: preset e codecs podiam voltar a valores inesperados.

## Refinamentos da versão 2.2

- O arrastar e soltar agora é aceito no card, na área pontilhada e na janela da ferramenta ativa.
- Campos de tamanho usam número inteiro separado da unidade KB, MB ou GB, eliminando as setas apertadas.
- O formulário recebeu mais espaço vertical e horizontal entre rótulos, campos e controles.
- Divisão, compactação e conversão exibem estimativas antes de adicionar a tarefa à fila.
- A marca na barra lateral virou um bloco visual próprio, com hierarquia tipográfica e descrição das três ações.
- A versão 2.2.1 impede compressão vertical dos cards: em alturas menores a página usa rolagem, sem sobrepor ou cortar campos.
- A lista mostra três linhas completas e usa rolagem própria; nomes longos são abreviados sem alargar a interface.
- As três páginas foram verificadas em 1280×900 com escala de interface de 100% e 150%.
- O aplicativo passa a abrir sempre diretamente em “Compactar”.
- Corrigida a conversão de valores de `QComboBox`: ações não geram mais exceção ao salvar presets, modos ou codecs.
- Cada ferramenta possui uma ação primária curta para executar agora e uma ação secundária discreta para apenas adicionar à fila.
- A fila ganhou o comando “Iniciar fila (N)” e permite cancelar uma tarefa antes mesmo de iniciar.

## Melhorias aplicadas

- Novo símbolo “Helium Atom”: duas órbitas/dois elétrons e um núcleo com play, em azul Fluent e violeta.
- Lista compacta com mensagem central “Solte seus vídeos aqui”, tamanho do arquivo e rolagem quando necessário.
- Botões separados da área de drop, com hierarquia e espaçamento consistentes.
- Controles limitados a uma largura confortável e chevrons visíveis.
- Quantidade de partes transformada em seleção fechada de 2 a 10.
- Rótulo contextual muda entre “Quantidade”, “Duração por parte” e “Tamanho por parte”.
- Campo personalizado aparece somente no preset “Personalizada”.
- “Manter áudio original” ativado por padrão.
- VP8 selecionado por padrão e descrito como opção rápida; VP9 permanece disponível para maior eficiência.
- Cada tela informa onde os arquivos serão salvos e explica seus principais trade-offs.
- Fila ganhou estado vazio instrutivo e cards claros para running/success/error/canceled.
- FFmpeg detectado usa verde semântico; foco de teclado e barras de rolagem receberam tratamento visual.
- Preferências de preset, codec, áudio, precisão, página e tema são persistidas.
- Som de conclusão ocorre uma única vez quando toda a fila termina.

## Critérios de UX preservados

- O usuário recebe feedback assim que adiciona uma tarefa.
- A janela nunca executa FFmpeg na thread principal.
- Erros continuam em linguagem humana; detalhes técnicos ficam no log.
- Arquivos existentes nunca são sobrescritos silenciosamente.
- Tema automático, claro, escuro e alto contraste permanecem suportados.
