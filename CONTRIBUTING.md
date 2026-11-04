# Como contribuir

A versão atual é um aplicativo Windows em C#/.NET 10 com WPF. O código Python na raiz registra a versão anterior da aplicação.

| Pasta | Conteúdo |
| --- | --- |
| [src/HeliumVideoTools.Core](src/HeliumVideoTools.Core) | Modelos, contratos e regras de vídeo |
| [src/HeliumVideoTools.Infrastructure](src/HeliumVideoTools.Infrastructure) | Processamento, atualizações, armazenamento e recuperação |
| [src/HeliumVideoTools.Desktop](src/HeliumVideoTools.Desktop) | Interface WPF |
| [src/HeliumVideoTools.UpdateHost](src/HeliumVideoTools.UpdateHost) | Auxiliar de atualização |
| [tests](tests) | Testes de processamento e interface |
| [installer](installer) e [scripts](scripts) | Empacotamento e distribuição |

## Executar no Windows

Instale o SDK do .NET 10 e use o PowerShell na raiz do repositório.

```powershell
.\scripts\Prepare-MediaTools.ps1
dotnet build src/HeliumVideoTools.Desktop -c Release
dotnet run --project src/HeliumVideoTools.Desktop
```

## Testar e propor mudanças

1. Descreva o problema, a versão do app e os passos para reproduzi-lo. Use amostras sintéticas para casos de mídia.
2. Trabalhe em uma branch e mantenha a alteração focada. Para mudanças visuais, anexe capturas de antes e depois.
3. Execute os testes pertinentes. A suíte principal usa os binários preparados no passo anterior.

```powershell
dotnet run --project tests/HeliumVideoTools.Tests -c Release
```

4. Abra uma solicitação de alteração com o comportamento esperado e os testes realizados. O [fluxo de validação do Windows](.github/workflows/windows.yml) lista as verificações de interface e instalação.

Para falhas de segurança, siga a [política de segurança](SECURITY.md).
