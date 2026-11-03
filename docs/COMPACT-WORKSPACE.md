# Interface compacta em dois painéis — 3.0.15

Modelo B aprovado em 03/11/2026, após esclarecer a diferença entre a letra citada e a imagem anexada. Refinamentos aprovados depois de prévia: área de soltar ampliada, botões Adicionar/Remover/Limpar iguais e Áudio/Precisão lado a lado.

## Comportamento

- Dividir, Compactar e Converter compartilham arquivos à esquerda e opções à direita. A área escura preenche a altura restante do cartão; os três botões têm colunas iguais e intervalos de 8 unidades.
- Drag & drop de arquivos usa eventos de prévia da janela, antes dos handlers de TextBox/ListBox. Funciona sobre a lateral, campos, seletores e cartões da área interna. Não altera o conteúdo de campos numéricos. Texto comum continua usando o comportamento nativo. Arquivos ausentes/não suportados e duplicados seguem o filtro existente.
- Durante processamento, seleção e configuração ficam bloqueadas, inclusive drops. O progresso substitui a barra de execução; ao concluir, aparecem relatório, pasta e Voltar ao início.
- Voltar ao início limpa somente a sessão em memória e retorna a Compactar/Personalizada/98 MB/áudio original e padrões de precisão/divisão. Preserva tema, ferramentas, preferências de janela/pasta e arquivos/relatórios em disco. Não executa durante processamento, fechamento ou instalação.
- Alterar a seleção depois de um resultado retorna à edição sem perder a seleção; permite iniciar outro lote. Não há fila de tarefas.
- Redimensionar preserva controles e valores: dois painéis quando há largura; painéis empilhados em janela estreita; rótulos laterais em áreas baixas. A área útil e a escala do Windows determinam o espaço real. Em dimensões extremas, mantém acesso por rolagem, em vez de cortar controles. Listas extensas de arquivos mantêm sua navegação interna.

## Evidências

`--verify-compact-workspace work/compact-workspace-validation` verifica roteamento real dos eventos WPF sobre janela, lateral, lista, campo e seletor nas três operações; bloqueio durante lote; duplicatas; preservação do campo; texto comum; reset e preservação de saída; nova seleção após resultado. Arquivos de teste são artificiais e ficam em work.

O mesmo teste alterna início/progresso/resultado e áreas lógicas 1120×700, 1280×650, 1280×720, 1536×800, 1920×1000, 900×650 e 675×449. Usa estimativas com resumo e explicação completos. Confere ausência de rolagem geral nas áreas de 900×650 ou maiores desse conjunto, cartões/ações dentro da janela, botões com larguras iguais, ausência de espaço vazio sob botões e preservação de valores/instâncias. No caso estreito, exige reorganização dos painéis. Isso não declara validação física de todas as escalas ou ausência de rolagem para qualquer tamanho/texto possível.

Também continuam exigidos os testes existentes de temas/alto contraste, menus/foco/teclado, layout mínimo reversível, fechamento e resumo recuperado. Não há alteração do motor, dos limites de tamanho ou da distribuição por usuário.
