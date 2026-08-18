//namespace define o local onde a classe está localizada, neste caso, dentro do namespace NajGravador.Models
namespace NajGravador.Models;

//aqui criamos a classe audiorecording, que define as propriedades da gravação de áudio
public class AudioRecording
{ //guid.newguid() gera um id com varias letras/numeros, ToString() converte o guid para string.
  //get, set permitem acessar e ler/modificar os valores da propriedade.
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    //filePath é o caminho do arquivo de áudio gravado, criado no momento da gravação.
    public string FilePath { get; set; } = string.Empty;
    //datetime.now pega a data e hora atual, e atribui a propriedade createdat, que indica quando a gravação foi criada.
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    //timespan comeca a contar desde zero, e é usado para armazenar a duração da gravação de áudio.
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
}