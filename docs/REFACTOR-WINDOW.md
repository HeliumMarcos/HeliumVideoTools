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

Repetir suíte de mídia/recuperação/arquitetura, fechamento, temas e distribuição antes de publicar, além do benchmark para observar possíveis regressões. Alterações visuais futuras continuam exigindo a prévia/aprovação do usuário.
