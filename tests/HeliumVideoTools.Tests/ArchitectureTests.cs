using HeliumVideoTools.Core;
using HeliumVideoTools.Infrastructure;

internal static class ArchitectureTests
{
    internal static void Run()
    {
        var domain=typeof(VideoOptions).Assembly;
        var services=typeof(VideoEngine).Assembly;
        if(domain==services)throw new Exception("Motor com efeitos externos ainda está na mesma assembly dos modelos/regras");
        if(typeof(VideoEstimates).Assembly!=domain || typeof(UserPreferences).Assembly!=domain || typeof(IVideoProcessor).Assembly!=domain)
            throw new Exception("Contratos/regras saíram do núcleo");
        if(new[]{typeof(ReleaseClient),typeof(RecoveryJournal),typeof(UserPreferenceStore),typeof(BatchReportStore)}.Any(type=>type.Assembly!=services))
            throw new Exception("Serviços externos fora da infraestrutura");
        if(domain.GetReferencedAssemblies().Any(name=>name.Name!.StartsWith("HeliumVideoTools.") || name.Name.StartsWith("Presentation") || name.Name.Contains("Registry")))
            throw new Exception("Núcleo depende de infraestrutura/GUI/registro");
        if(!services.GetReferencedAssemblies().Any(name=>name.Name==domain.GetName().Name))throw new Exception("Infraestrutura não depende do contrato do núcleo");
        Console.WriteLine("PASS: núcleo sem dependência da infraestrutura/WPF/registro; contratos/regras e serviços em assemblies separadas.");
    }
}
