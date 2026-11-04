# Auditoria e modernização

## Problemas encontrados no código legado

- Interface, regras de negócio, comandos FFmpeg, estado e empacotamento estavam concentrados em `app_final.py`.
- `subprocess.run` era usado para inspeção de mídia e poderia bloquear quando chamado pelo fluxo errado.
- Threads nativas atualizavam widgets indiretamente com `after`, sem um contrato tipado de sinais e sem fila central.
- Cancelamento usava apenas `terminate`, sem escalonamento para `kill` e sem tratar tarefas ainda na fila.
- Erros por arquivo eram absorvidos em alguns lotes; o lote podia terminar com aparência de sucesso.
- A leitura do progresso dependia do texto de diagnóstico do FFmpeg e da localização de `time=`.
- Havia mistura de `os.path` e `pathlib`, constantes globais e caminhos específicos do ambiente.
- Não existiam logs rotativos, persistência de preferências, verificação prévia de espaço ou nomes de saída não destrutivos.
- Os botões da lista de arquivos estavam indentados dentro de `_on_drag_leave`; podiam não ser construídos.

## Arquitetura nova

- `core/`: modelos tipados, cálculo de bitrate, criação de comandos, processo FFmpeg cancelável e processamento.
- `ui/`: janela PySide6, páginas, cards, drag-and-drop, tema Fluent e efeitos Windows 11.
- `utils/`: descoberta de assets/binários e logging JSON rotativo.
- `config/`: preferências estáticas com `dataclass`; preferências do usuário ficam no `QSettings`.
- `assets/`: logo, variantes, ícones vetoriais e futuros QSS/traduções.
- `tests/`: testes das regras de bitrate, comandos e nomes de saída.

As tarefas usam `QThreadPool`/`QRunnable`. Nenhum `subprocess.run` existe na aplicação; FFmpeg e FFprobe usam `Popen` com `CREATE_NO_WINDOW` no Windows. O protocolo `-progress pipe:1` alimenta progresso e ETA em tempo real. O cancelamento encerra o processo e remove apenas a saída parcial atual.

## Distribuição e inicialização

A distribuição final usa `onefile` sem o splash experimental baseado em Tk. Ela é construída num ambiente Python 3.12 isolado, compatível com o runtime Qt empacotado. O FFprobe de aproximadamente 100 MB foi removido e a inspeção de mídia passou a usar o próprio FFmpeg, diminuindo o volume extraído na inicialização.
