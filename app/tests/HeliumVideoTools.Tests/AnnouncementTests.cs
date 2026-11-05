using HeliumVideoTools.Desktop;

internal static class AnnouncementTests
{
    internal static void Run()
    {
        long time=0;var gate=new AnnouncementGate(()=>time);
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        Check(!gate.Accept(" "),"Anúncio vazio aceito");
        Check(gate.Accept("Primeiro"),"Primeiro progresso não anunciado");
        for(int i=0;i<1000;i++){time=i;Check(!gate.Accept("Progresso "+i),"Progresso gerou excesso de anúncios");}
        time=4999;Check(!gate.Accept("Aguardando"),"Intervalo mínimo não respeitado");
        time=5000;Check(gate.Accept("Segundo"),"Progresso após intervalo não anunciado");
        Check(!gate.Accept("Segundo",true),"Anúncio repetido");
        time=5001;Check(gate.Accept("Concluído",true),"Resultado final aguardou intervalo");
        gate.Reset();Check(gate.Accept("Concluído"),"Novo lote herdou supressão do anterior");
        Check(gate.Accept("Erro",true),"Erro final suprimido");
        Check(gate.Accept("Cancelado",true),"Cancelamento final suprimido");
        Console.WriteLine("PASS: primeiro anúncio, limite de 5 s, supressão de repetição, resultado/erro/cancelamento imediato e reinício por lote.");
    }
}
