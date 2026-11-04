# Helium Vídeo Tools 3.0.17

- Página do projeto reorganizada, com download da versão estável mais recente, guia de uso e documentação em português brasileiro.
- Guia de licenças e avisos em português incluído na pasta do aplicativo instalado, junto aos termos originais das dependências.
- A licença do FFmpeg distribuída no pacote vem do mesmo arquivo compactado que fornece os executáveis.


# Helium Vídeo Tools 3.0.16 — ajustes de áudio na compactação

- Ao desligar Áudio original, aparecem Taxa de áudio e Canais, lado a lado.
- Recodificação AAC com 64 a 320 kbps e canais originais, mono ou estéreo; padrão 128 kbps/manter canais.
- Motor e estimativa consideram a taxa selecionada no orçamento de vídeo, respeitando o teto final.
- Áudio original ligado continua copiando o som sem alteração; vídeos sem áudio continuam sem som.
- Ajustes capturados por lote, campos protegidos durante o processamento e retorno à edição ao mudar parâmetros depois do resultado.
- Adaptação para manter os controles visíveis nos tamanhos verificados, inclusive com progresso/resultado e estimativa completa.

Testes com FFmpeg/ffprobe reais conferem taxa, canais, uma/duas etapas, limite, decodificação e cópia AAC idêntica. Detalhes em docs/AUDIO-SETTINGS.md.

## Histórico da versão anterior

# Helium Vídeo Tools 3.0.15 — interface compacta e arrastar em toda a janela

- Novo layout aprovado em dois painéis para Dividir, Compactar e Converter, adaptável ao tamanho da janela e sem rolagem geral nos tamanhos usuais verificados.
- Área de soltar ampliada, botões iguais e alinhados; Áudio e Precisão lado a lado.
- Solte vídeos sobre a lateral, campos e demais áreas internas da janela. Durante o processamento, a seleção fica protegida.
- Progresso e resultado mais compactos; botão Voltar ao início limpa a sessão e retorna a Compactar/98 MB sem apagar vídeos ou resultados.
- Selecionar novos arquivos após concluir volta à edição e permite novo processamento.
- Mantidos temas, teclado, nomes acessíveis, limites de tamanho, instalação por usuário e atualizações pelo app.

Em janelas extremamente pequenas, a adaptação preserva acesso por rolagem em vez de cortar campos. Evidências e limites em docs/COMPACT-WORKSPACE.md.

## Histórico da versão anterior

# Helium Vídeo Tools 3.0.14 — histórico recuperado após interrupção

- Lotes registram início, resultados individuais e saídas publicadas fora da instalação. Lote ativo é protegido por bloqueio exclusivo.
- Ao reabrir após interrupção, gera relatório do histórico e restaura o resumo mais recente com relatório/pasta, preservando saídas concluídas. Não retoma codificação nem cria fila.
- Saídas ausentes/alteradas são identificadas. Metadados inválidos e arquivos desconhecidos são preservados; falha ao gravar relatório permite repetir recuperação.
- Teste encerra worker real durante o lote e verifica recuperação, estados e ausência de relatórios duplicados.
- Verificações de teclado, eventos acessíveis, alto contraste e renderização do resumo em 100/125/150/200% reunidas nesta entrega. Interface existente preservada.

Escuta do Narrador fica pendente por escolha do usuário. Renderização não equivale a teste físico de escalas/monitores mistos; esse cenário depende de ambiente adequado. Distribuição somente no GitHub. Detalhes/limites em docs/BATCH-RECOVERY.md.

## Melhorias preservadas da 3.0.13

- Eventos de região dinâmica no progresso: primeiro evento, atualizações com intervalo mínimo de cinco segundos e resultado/erro/cancelamento sem aguardar esse intervalo. Textos repetidos são suprimidos; o leitor decide quando falar, respeitando Polite.
- Erro de validação com painel oculto usa o status acessível do rodapé. Visual, conteúdo e foco preservados.
- Cliente de acessibilidade separado confirma recebimento de eventos e textos, incluindo resultados reais da janela e rajada de progresso sem excesso.
- Tab/Shift+Tab verificados nas três ferramentas, incluindo acesso a Cancelar com os campos desabilitados.
- Corrigido isolamento dos testes WPF: evitam abertura automática de uma segunda janela normal. Benchmark antigo de abertura/memória fica como histórico; nova amostra exige uma única janela.

Sete imagens antes/depois idênticas. Um monitor real em 150% foi verificado localmente; troca física entre escalas diferentes e áudio do Narrador continuam pendentes. Instalador somente nas Releases, sem cópia na pasta Music. Detalhes em docs/ACCESSIBLE-PROGRESS.md.

## Melhorias preservadas da 3.0.12

- Janela ajustada à área útil do monitor, com declaração PerMonitorV2 e resposta a mudança de DPI/monitor/área de trabalho.
- Formulário e lateral com rolagem quando necessário; em janelas estreitas, rótulos acima dos campos e ação principal em largura maior.
- Em áreas muito baixas, ou baixas com progresso visível, o espaço de trabalho inteiro pode rolar até a ação. Layout amplo restaurado ao aumentar, preservando campos e valores.
- Botões Adicionar/Remover/Limpar e barra de progresso com nomes acessíveis explícitos.
- Proposta antes/depois aprovada pelo usuário. Testes de áreas lógicas, estados, reflow reversível e ajuste nativo ao monitor incluídos no GitHub.

Não altera codecs, limites, processamento ou padrões de abertura. Validação física de movimentação entre monitores com DPI diferente e uso com leitor de tela continuam pendentes; simulação não é apresentada como validação desses cenários. Instalador somente no GitHub, sem nova cópia na pasta Music.

## Melhorias preservadas da 3.0.11

- Consulta inicial de atualização em coordenador próprio: ocorre uma vez, após a recuperação, e descarta respostas recebidas depois de fechar.
- Janela de Atualizações reutilizada enquanto aberta e liberada ao fechar, permitindo reabertura imediata.
- Encerramento separado dos controles: confirmação durante processamento, cancelamento, espera e salvamento das preferências antes do fechamento agendado.
- Testes cobrem chamadas repetidas, falhas de rede/gravação e fechamento normal ou solicitado pelo instalador.
- Visual preservado: sete imagens idênticas antes/depois, comparadas antes de alterar a versão. Teste de temas aguarda o estado habilitado/desabilitado se estabilizar, sem mudar estilos.

Instalador por usuário, mesma pasta, ferramentas e parâmetros de vídeo preservados. Esta entrega conclui as extrações de atualização/encerramento previstas para a janela principal; validação de telas, DPI e acessibilidade segue no plano.

## Melhorias preservadas da 3.0.10

- Coordenação das análises em componente próprio, mantendo a espera de 250 ms, cache de metadados e uso do conjunto capturado de ferramentas.
- Testes verificam que respostas ou erros atrasados não substituem a análise atual ao mudar parâmetros, limpar arquivos, navegar ou fechar.
- Consulta com FFprobe real na janela padrão e mínima incluída na validação do GitHub.
- Aparência e padrões preservados: sete imagens de temas idênticas antes/depois, comparadas antes do incremento da versão. Sem alteração de codecs ou comandos de processamento.

Instalação por usuário, mesma pasta, atualização pelo aplicativo e recuperação preservadas. A organização interna continua: atualização e encerramento ainda são coordenados pela janela. Detalhes em docs/REFACTOR-WINDOW.md.

## Melhorias preservadas da 3.0.9

- Configuração das operações fica em componente próprio: campos, unidades, modos de divisão, validação e preferências compatíveis.
- Formatação do resultado do lote separada dos controles, preservando contagens, mensagens, cores e avisos de relatório.
- Janela principal fica menor, com responsabilidades mais claras; os eventos continuam coordenando tarefas e aplicação dos resultados à interface.
- Testes de caracterização verificam os padrões de 98 MB, áudio original, VP8, presets, codecs e divisão de 2 a 10 partes.
- Sete renderizações antes/depois tiveram hashes idênticos, antes da alteração do número da versão. Não há mudança visual ou de fluxo nesta entrega.

Instalador por usuário, pasta fixa, atualizações, recuperação e dados preservados. Esta é mais uma etapa do item 6, não a conclusão de toda a revisão da janela. Benchmark e validações em docs/REFACTOR-WINDOW.md e docs/DESEMPENHO.md.

## Melhorias preservadas da 3.0.8

- Modelos, contratos, preferências tipadas e regras de orçamento ficam no núcleo Core, sem dependência do WPF ou da infraestrutura.
- Execução de FFmpeg/ffprobe, recuperação, atualizações, registro de preferências, relatórios e logs ficam no projeto Infrastructure, que referencia o núcleo.
- Execução de processos e sondagem de metadados foram extraídas do motor, preservando comandos, timeouts, mensagens e métodos de entrada.
- Novo teste impede reunir novamente os serviços externos e as regras na mesma assembly ou criar dependência inversa do núcleo.
- Sem mudanças visuais, de formato das preferências/relatórios ou dos padrões de 98 MB/VP8. Instalador por usuário, mesma pasta e atualização via canal estável preservados.

Testes de mídia, interrupções de processos/atualizações, cancelamento, preferências, temas e fechamento verificam o comportamento após a separação. Benchmark repetido com a mesma amostra sintética e parâmetros; esta entrega não promete aceleração de codificação. A redução das responsabilidades do código da janela continua em etapa posterior.

## Melhorias preservadas da 3.0.7

- Download e extração do FFmpeg/ffprobe agora têm registro de transação bloqueado enquanto a atualização está ativa. Na abertura, restos de tentativas interrompidas podem ser limpos sem apagar o conjunto ativo/anterior.
- Controle ou executável ativo danificado é validado e, quando há um ponto anterior válido, restaurado automaticamente. Na primeira atualização, o retorno pode ser ao conjunto incluído no instalador.
- Se o instalador for encerrado no intervalo entre guardar o app anterior e ativar o novo, executá-lo novamente restaura a pasta anterior antes de continuar. Pastas residuais inesperadas são preservadas, sem sobrescrever seu conteúdo.
- Novos testes matam processos de atualização em oito pontos/condições. O instalador também recebe testes de interrupção em compilação isolada, com payload controlado e sem acesso aos atalhos/registro do produto real.
- Sem mudanças no layout, nos temas ou nos padrões de 98 MB/VP8. Instalação por usuário, sem administrador, na mesma pasta.

Os testes controlam encerramento de processos; não desligam o computador e não atestam uma queda física de energia. Quando o executável fica ausente durante a troca interrompida, é necessário executar o instalador novamente. A retomada automática de codificação/histórico não foi adicionada.

## Melhorias preservadas da 3.0.6

- Novos temporários de vídeo e estatísticas de codificação são registrados antes da criação, com gravação do registro em disco e bloqueio enquanto a operação está ativa.
- Na abertura, uma recuperação em segundo plano limpa apenas temporários registrados de tarefas interrompidas. Originais, resultados publicados e conteúdos desconhecidos são preservados.
- Processos de vídeo são vinculados a um Job Object do Windows com encerramento ao fechar o último identificador. Isso reduz o risco de FFmpeg órfão após encerrar o aplicativo à força, sem procurar e matar processos pelo nome.
- Diretórios redirecionados são resolvidos ao registrar os temporários; a recuperação recusa caminhos posteriormente redirecionados.
- Sem alteração visual; temas, padrões de 98 MB/VP8 e instalador por usuário preservados.

Teste isolado encerra somente o hospedeiro enquanto FFmpeg grava um MP4, verifica a saída do processo vinculado e mantém um segundo FFmpeg independente. Também cobre bloqueio durante tarefa ativa, cancelamento da recuperação, repetição, JSON danificado e preservação de arquivos concluídos/desconhecidos.

A recuperação faz limpeza; não retoma automaticamente uma codificação de onde parou. Abrange os novos temporários registrados, sem tentar adivinhar a propriedade de arquivos de versões antigas. Queda real de energia e interrupções do instalador/atualização de ferramentas continuam em validação separada.

## Melhorias preservadas da 3.0.5

- Proposta visual aprovada integrada: seletor Sistema (Windows), Claro e Escuro na lateral. A escolha é lembrada sem alterar os padrões de processamento.
- Sistema acompanha a aparência do Windows. O alto contraste do Windows tem prioridade e usa suas cores, inclusive em seleções e botões.
- Marca com variante adequada ao fundo; barra de título, campos, menus, estimativas, resultados e janela Atualizações acompanham o tema sem reiniciar.
- Foco de teclado mais visível, ordem de Tab na navegação e nomes acessíveis dos controles revisados. Trocar o tema preserva os valores digitados.
- Atualizações mantém o layout compacto, com progresso e cancelamento, sem rolagem no tamanho padrão nos estados testados.

Mantidos Compactar ao abrir, Personalizada/98 MB, áudio original ligado e WebM VP8. Instalação por usuário, sem administrador, na mesma pasta; atualização pelo aplicativo via Releases estáveis.

Testes cobrem as três ferramentas em 1280×900 e 980×700 DIPs, cores de contraste simuladas sem modificar o Windows, menus, Tab/foco, preferências e estados de Atualizações. Isso não substitui testes com leitor de tela, todas as paletas personalizadas nem movimentação real entre monitores com DPI diferente.

## Melhorias preservadas da 3.0.4

- A primeira atualização do FFmpeg/ffprobe agora guarda um ponto de retorno às ferramentas incluídas no instalador.
- Um controle de ferramentas danificado pode ser reparado instalando novamente o conjunto pela janela Atualizações, preservando o ponto de restauração existente.
- Processamento e estimativas capturam os dois executáveis do mesmo conjunto imutável e validam esse conjunto antes de usá-lo.
- Cancelamento, pacote incompleto, versão divergente e hash inválido não ativam o conjunto novo; diretórios incompletos dessa tentativa são removidos.
- Restauração valida os hashes do conjunto anterior antes de ativá-lo. Metadados de controle têm validação de identidade, tamanho e JSON; gravações são atômicas.
- Instalador envia uma solicitação explícita de encerramento para a janela, inclusive oculta/minimizada, para usar o fluxo normal de gravação das preferências antes de substituir o aplicativo. O encerramento forçado permanece como último recurso após o prazo.

Mantidos os padrões de abertura: Compactar, Personalizada/98 MB, áudio original e WebM VP8. Atualização por usuário, sem administrador, na mesma pasta.

Testes em perfil isolado usam FFmpeg/ffprobe reais com transporte HTTP simulado, sem modificar a instalação do usuário. Incluem instalação, restauração embutida/anterior, cancelamento, corrupção e preservação de um conjunto já capturado. Isso não simula uma queda de energia nem substitui o teste de download das ferramentas pela rede real.

## Melhorias preservadas da 3.0.3

- Apresentação das estimativas aprovada: separa orçamento dos codecs, teto aplicado e referência de tamanho baseada no original.
- Compactação e WebM usam as mesmas regras de orçamento do motor. Um original de 40 MB não recebe um orçamento de 299 MB na conversão.
- Divisão por quantidade/duração mostra referência por parte e duração, sem somar todos os originais como se fossem uma única parte. A referência não promete o tamanho após recodificar. Divisão por tamanho mostra o teto, sem inventar uma quantidade final de partes.
- Metadados analisados em segundo plano, com cache por tamanho/data, até duas sondagens paralelas e cancelamento das solicitações antigas. Detalhes por arquivo no tooltip.
- Preferências úteis persistem fora da instalação: última pasta escolhida, dimensões/maximização, partes/minutos da divisão e modo em duas etapas. Importação inicial de opções compatíveis da versão Python, sem modificar o registro legado.
- Mantidos os padrões exigidos: abrir em Compactar, Personalizada/98 MB, áudio original ligado e conversão VP8. Tamanho-alvo antigo, codec, preset e página inicial não são restaurados.
- Configuração inválida é preservada em backup e não impede iniciar com os padrões.

O valor exibido é orçamento ou referência, não promessa de tamanho exato. O motor continua conferindo o tamanho real antes de publicar cada saída. Relatórios, preferências e vídeos ficam locais; não são enviados ao GitHub.

Instalação por usuário, sem administrador, na mesma pasta; .NET e FFmpeg/ffprobe incluídos. `SHA256SUMS.txt` acompanha o instalador; não há assinatura Authenticode configurada. Validações com audiências extensas, leitores de tela e monitores/DPI continuam no plano.
