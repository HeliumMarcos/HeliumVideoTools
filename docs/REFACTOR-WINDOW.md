# Janela principal — segunda etapa do item 6

Escopo: reduzir a concentração de responsabilidades sem alterar aparência, textos, controle de tarefas, formato de preferências ou parâmetros de vídeo. A coordenação de atualização/encerramento e a análise assíncrona de estimativas continuam na janela nesta etapa; não se declara o item 6 inteiro concluído.

## Extrações

- `OperationEditor` controla criação dos campos, espaçamento/recursos, visibilidade dos modos, validação numérica, unidades, leitura de VideoOptions e opções compatíveis a lembrar. Não inicia tarefas, processos, sondagens nem grava preferências. A janela chama Build/Read/Remember e recebe um callback de mudança para solicitar estimativa.
- `BatchResultPresentation` transforma BatchResult e eventual aviso de relatório em título, contagens, chave semântica de cor, detalhe e status. Não acessa controles, disco ou estado mutável do lote.
- `MainWindow` mantém eventos, composição, processamento/fechamento e aplicação dos dados de apresentação aos controles. O arquivo principal caiu de 334 para 235 linhas não vazias nesta entrega; o código não foi simplesmente distribuído em arquivos partial da mesma classe.

## Evidências

Antes da extração, o novo teste de caracterização passou sobre a implementação antiga. Depois da extração, passou novamente: três operações, KB/MB/GB, campo vazio, preset com alvo oculto, H.264/H.265, VP8/VP9, partes de 2 a 10, duração, preferências compatíveis e padrões protegidos. Cobertura dos estados de resultado inclui sucesso, erro, misto, cancelado e aviso de relatório.

Antes de alterar a versão, as renderizações dos testes de temas foram executadas em `work/editor-before` e `work/editor-after`. Os SHA-256 dos sete PNGs coincidiram: dark, light, contrast-black, contrast-white e updates-dark/light/contrast-black. Isso comprova igualdade das imagens geradas nesses cenários, não de todos os estados possíveis. Nenhum XAML ou recurso de estilo foi alterado.

Testes que precisavam manipular controles do editor usam seu acesso interno de teste, em vez de procurar campos privados removidos da janela. A interface não recebe API pública adicional para essa finalidade. O novo teste de caracterização integra o workflow Windows antes do empacotamento.

A [execução 41385183429](https://github.com/HeliumMarcos/HeliumVideoTools/actions/runs/41385183429) passou na suíte de mídia/recuperação/arquitetura, fechamento, temas, editor e distribuição, publicando a [3.0.9 estável](https://github.com/HeliumMarcos/HeliumVideoTools/releases/tag/v3.0.9). O cliente real baixou anonimamente o instalador de 172908312 bytes e validou SHA-256/MZ; a consulta ofereceu atualização para a 3.0.8 e demais versões anteriores testadas, mas não para a própria 3.0.9. Benchmark comparativo em DESEMPENHO.md. Alterações visuais futuras continuam exigindo a prévia/aprovação do usuário.

## Continuação 3.0.10 — ciclo de vida das estimativas

EstimateCoordinator assume cancelamento/substituição das solicitações, espera de 250 ms, captura das ferramentas, reutilização do VideoEstimateService por caminho de ffprobe e conversão em EstimatePresentation. A janela aplica os mesmos textos, tooltip, ajuda acessível e visibilidade. Cancelar invalida a solicitação antes de sinalizar o token; cada solicitação descarta sua própria fonte de cancelamento ao terminar. Fechar a janela cancela pelo token de vida já existente.

Testes controlados usam respostas que ignoram cancelamento: sucesso e falha antigos não publicam sobre a solicitação nova; espera cancelada não inicia sondagem; limpar/navegar e encerrar descartam resposta tardia; opções inválidas preservam a exceção para a mensagem humana existente. A suíte principal inclui esses casos. O teste WPF com FFprobe real passou localmente em 1280×900 e 980×700 e passa a integrar o workflow.

Comparação local antes/depois, ainda na versão 3.0.9: sete PNGs em work/coordinator-before e work/coordinator-after têm hashes idênticos. Nenhum XAML/estilo foi alterado. Atualização e encerramento ainda precisam da etapa de separação; isso não conclui o item 6. Não foi repetido o benchmark de codificação nesta etapa: os comandos/motor não mudaram e não se alega ganho de desempenho.
