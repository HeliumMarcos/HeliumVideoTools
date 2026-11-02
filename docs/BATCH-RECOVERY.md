# Recuperação de lote e verificação de acessibilidade — 3.0.14

O usuário pediu os itens 2 e 3 juntos e, durante a execução, escolheu deixar a escuta do Narrador pendente. Esta entrega fecha a implementação de recuperação de histórico e reúne as verificações automatizadas; não declara concluídos testes físicos indisponíveis no ambiente.

## Recuperação de histórico

BatchRecoveryStore registra cada lote no perfil, fora da instalação (`batches` junto a `reports`). Um arquivo de lease exclusivo impede que outro processo trate o lote ativo como abandonado. Um snapshot JSON com versão e ID guarda fontes, início/conclusão de cada item e saídas publicadas, com tamanho/data. Gravação em temporário com Flush(true) e troca atômica mantém o snapshot anterior até a substituição. Há limites de schema, quantidade e tamanho; metadados inválidos são preservados e ignorados.

BatchRunner registra início, publicações e resultado individual. O ViewModel faz registro/processamento fora da thread WPF; ao gravar o relatório normal, remove o snapshot. Se a gravação do relatório falhar, o registro permanece para a abertura seguinte. Não altera codecs nem o motor de vídeo.

Ao abrir, snapshots sem lease ativo geram relatórios determinísticos por ID. O snapshot só é removido depois de salvar JSON e TXT; repetir após uma falha não cria relatórios com nomes diferentes. A recuperação não lê nem modifica o conteúdo dos originais, não apaga vídeos e não reinicia FFmpeg. Saídas só são oferecidas quando ainda existem com tamanho/data registrados e sem redirecionamento no caminho; isso não é nova validação audiovisual nem hash integral. Saídas ausentes/alteradas são relatadas.

Concluídos permanecem concluídos; item iniciado sem resultado final é cancelado com motivo de interrupção inesperada; demais ficam não iniciados. Essa representação usa os estados e a apresentação já existentes. O relatório explicita que foi recuperado após interrupção. O resumo mais recente é restaurado com Ver relatório/Abrir pasta; os demais relatórios ficam na pasta reports. Resultado novo/ativo não é substituído por recuperação tardia. Não há fila nem retomada automática de codificação.

## Evidências

- Teste escrito antes da implementação falhou pela ausência de BatchRecoveryStore, depois passou.
- Worker executa BatchRunner, publica a primeira saída e parte do segundo item, mantém terceiro não iniciado. O teste confirma que um lote ativo não é recuperado, mata somente esse worker e verifica os estados/saídas/relatório. Recuperação repetida não duplica relatório.
- Saída alterada deixa de ser oferecida; metadado corrompido e arquivo externo são preservados. Falha ao criar relatório mantém o snapshot e permite nova tentativa.
- Teste WPF restaura resumo, relatório/pasta e ignora recuperação tardia. Captura o mesmo conteúdo a 96/120/144/192 DPI (100/125/150/200%) sem modificar escala/resolução do Windows.
- Teclado, eventos UIA entre processos, layout reversível, monitor disponível em 150%, temas/duas paletas customizadas, fechamento, mídia e recuperações anteriores continuam na suíte.

## Limites explícitos

Há um intervalo entre publicar uma saída no motor e registrar seu caminho; se o processo morrer exatamente nele, o arquivo concluído permanece no disco, mas pode não aparecer no histórico. A recuperação não faz varredura das pastas de vídeos para adivinhar propriedade. Tamanho/data não detectam uma alteração deliberada que preserve ambos. Relatórios recuperados ficam locais e contêm caminhos; não são enviados ao GitHub.

Flush/troca atômica e testes de morte de processo não garantem persistência após falha física de energia/armazenamento. Não se desliga o computador para simular esse cenário. A proteção de processos próprios já existente continua com a janela curta entre Start/Job Object registrada como limitação.

Escuta do Narrador foi adiada expressamente pelo usuário. Testes de renderização e UIA não substituem essa escuta nem a troca física entre monitores com escalas diferentes. O ambiente tem um monitor em 150%; 100/125/200% físicos e DPI misto seguem dependentes de ambiente adequado. Nenhuma cópia de instalador na pasta Music.

## Publicação e verificação

A [execução 41493104918](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41493104918) passou na suíte e na instalação/reinstalação/interrupção do instalador, publicando a [3.0.14 oficial](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.14). Inclui a morte real do worker BatchRunner, WPF de resumo recuperado, teclado/UIA, áreas menores, temas, processamento e recuperações anteriores.

A [verificação independente 41493942250](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41493942250) confirmou download público anônimo pelo cliente real (172961238 bytes, SHA-256/MZ) e detecção de atualização desde versões anteriores testadas, incluindo 3.0.13, sem oferta para a própria 3.0.14. Sem cópia de instalador na pasta Music.
