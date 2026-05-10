using System.IO;
using System.Xml.Linq;
using AnaliseDemonstrativoTISS.Operadoras;

namespace AnaliseDemonstrativoTISS.Operadoras.Petrobras;

public sealed class AnaliseXml
{
    private static readonly string[] TagsCredencial = ["numeroCarteira", "numeroCarteiraBeneficiario", "credencial", "matricula"];
    private static readonly string[] TagsNome = ["nomeBeneficiario", "nomePaciente", "nome"];
    private static readonly string[] TagsDataAtendimento = ["dataAtendimento", "dataRealizacao", "dataExecucao", "dataProtocolo", "dataPagamento"];
    private static readonly string[] TagsValorInformado = ["valorInformadoGuia", "valorInformado"];
    private static readonly string[] TagsValorProcessado = ["valorProcessadoGuia", "valorProcessado"];
    private static readonly string[] TagsValorLiberado = ["valorLiberadoGuia", "valorLiberado"];
    private static readonly string[] TagsValorGlosa = ["valorGlosaGuia", "valorGlosa"];

    public IReadOnlyList<RegistroAnalisePetrobras> Analisar(string caminhoXml, CampoFiltroPetrobras campoFiltro, IEnumerable<string> valoresFiltro)
    {
        if (string.IsNullOrWhiteSpace(caminhoXml))
        {
            throw new ArgumentException("O caminho do XML deve ser informado.", nameof(caminhoXml));
        }

        if (!File.Exists(caminhoXml))
        {
            throw new FileNotFoundException("Arquivo XML não encontrado.", caminhoXml);
        }

        var filtros = UtilitariosDeAnalise.CriarFiltros(valoresFiltro);
        var documento = XDocument.Load(caminhoXml, LoadOptions.None);
        var resultado = new List<RegistroAnalisePetrobras>();

        foreach (var guia in ObterGuias(documento))
        {
            var registro = CriarRegistro(guia);
            if (!UtilitariosDeAnalise.PassaFiltro(filtros, ObterValorCampoFiltro(campoFiltro, registro.NumeroGuiaPrestador, registro.NumeroGuiaOperadora, registro.Senha)))
            {
                continue;
            }

            resultado.Add(registro);
        }

        return resultado;
    }

    private static RegistroAnalisePetrobras CriarRegistro(XElement guia)
    {
        return new RegistroAnalisePetrobras
        {
            Credencial = BuscarPrimeiroValor(guia, TagsCredencial),
            Nome = BuscarPrimeiroValor(guia, TagsNome),
            Senha = BuscarPrimeiroValor(guia, "senha"),
            NumeroGuiaPrestador = BuscarPrimeiroValor(guia, "numeroGuiaPrestador"),
            NumeroGuiaOperadora = BuscarPrimeiroValor(guia, "numeroGuiaOperadora"),
            DataAtendimento = UtilitariosDeAnalise.FormatarDataPadrao(BuscarPrimeiroValor(guia, TagsDataAtendimento)),
            ValorInformado = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorInformado)),
            ValorProcessado = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorProcessado)),
            ValorLiberado = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorLiberado)),
            ValorGlosa = UtilitariosDeAnalise.FormatarValorMonetario(BuscarPrimeiroValor(guia, TagsValorGlosa)),
            TipoPagamento = BuscarPrimeiroValor(guia, "tipoPagamento"),
            CodigoGlosa = BuscarPrimeiroValor(guia, "codigoGlosa"),
            DescricaoGlosa = BuscarPrimeiroValor(guia, "descricaoGlosa")
        };
    }

    private static IEnumerable<XElement> ObterGuias(XDocument documento)
    {
        return documento.Descendants().Where(static elemento => NomeEh(elemento, "guiasDoLote"));
    }

    private static string ObterValorCampoFiltro(CampoFiltroPetrobras campoFiltro, string numeroGuiaPrestador, string numeroGuiaOperadora, string senha)
    {
        return campoFiltro switch
        {
            CampoFiltroPetrobras.NumeroGuiaPrestador => numeroGuiaPrestador,
            CampoFiltroPetrobras.NumeroGuiaOperadora => numeroGuiaOperadora,
            CampoFiltroPetrobras.Senha => senha,
            _ => string.Empty
        };
    }

    private static string BuscarPrimeiroValor(XElement origem, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            var encontrado = origem.DescendantsAndSelf().FirstOrDefault(elemento => NomeEh(elemento, nome));
            if (encontrado is null)
            {
                continue;
            }

            var valor = encontrado.Value.Trim();
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return string.Empty;
    }

    private static bool NomeEh(XElement elemento, string nome)
    {
        return string.Equals(elemento.Name.LocalName, nome, StringComparison.OrdinalIgnoreCase);
    }
}
