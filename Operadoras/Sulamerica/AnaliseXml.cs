using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace AnaliseDemonstrativoTISS.Operadoras.Sulamerica;

public enum CampoFiltroSulamerica
{
    Credencial,
    Nome,
    Senha
}

public sealed class RegistroAnaliseSulamerica
{
    public string Credencial { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
    public string NumeroGuiaPrestador { get; init; } = string.Empty;
    public string NumeroGuiaOperadora { get; init; } = string.Empty;
    public string DataAtendimento { get; init; } = string.Empty;
    public string CodigoProcedimento { get; init; } = string.Empty;
    public string DescricaoProcedimento { get; init; } = string.Empty;
    public string Quantidade { get; init; } = string.Empty;
    public string ValorInformado { get; init; } = string.Empty;
    public string ValorProcessado { get; init; } = string.Empty;
    public string ValorLiberado { get; init; } = string.Empty;
    public string ValorGlosa { get; init; } = string.Empty;
    public string SituacaoGuia { get; init; } = string.Empty;
    public string CodigoGlosa { get; init; } = string.Empty;
    public string DescricaoGlosa { get; init; } = string.Empty;
}

public sealed class AnaliseXml
{
    private static readonly string[] TagsCredencial = ["numeroCarteira", "numeroCarteiraBeneficiario", "credencial", "matricula"];
    private static readonly string[] TagsNome = ["nomeBeneficiario", "nomePaciente", "nome"];
    private static readonly string[] TagsSenha = ["senha", "senhaAutorizacao", "numeroSenha"];
    private static readonly string[] TagsDataAtendimento = ["dataAtendimento", "dataRealizacao", "dataExecucao", "dataInicioFat"];

    private static readonly string[] TagsValorInformadoGuia = ["valorInformadoGuia", "valorInformado"];
    private static readonly string[] TagsValorProcessadoGuia = ["valorProcessadoGuia", "valorProcessado"];
    private static readonly string[] TagsValorLiberadoGuia = ["valorLiberadoGuia", "valorLiberado"];
    private static readonly string[] TagsValorGlosaGuia = ["valorGlosaGuia", "valorGlosa"];

    private static readonly string[] TagsCodigoProcedimento = ["codigoProcedimento", "codigo", "procedimento"];
    private static readonly string[] TagsDescricaoProcedimento = ["descricaoProcedimento", "descricao"];
    private static readonly string[] TagsQuantidadeProcedimento = ["quantidadeExecutada", "quantidade"];
    private static readonly string[] TagsValorInformadoProcedimento = ["valorInformadoGuia", "valorInformado", "valorTotal", "valorProcedimento"];

    public IReadOnlyList<RegistroAnaliseSulamerica> Analisar(
        string caminhoXml,
        CampoFiltroSulamerica campoFiltro,
        IEnumerable<string> valoresFiltro)
    {
        if (string.IsNullOrWhiteSpace(caminhoXml))
        {
            throw new ArgumentException("O caminho do XML deve ser informado.", nameof(caminhoXml));
        }

        if (!File.Exists(caminhoXml))
        {
            throw new FileNotFoundException("Arquivo XML não encontrado.", caminhoXml);
        }

        var filtros = (valoresFiltro ?? [])
            .Select(Normalizar)
            .Where(static valor => !string.IsNullOrWhiteSpace(valor))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var documento = XDocument.Load(caminhoXml, LoadOptions.None);
        var resultado = new List<RegistroAnaliseSulamerica>();

        foreach (var guia in ObterGuias(documento))
        {
            var dadosGuia = ExtrairDadosGuia(guia);
            if (!PassaFiltro(campoFiltro, filtros, dadosGuia.Credencial, dadosGuia.Nome, dadosGuia.Senha))
            {
                continue;
            }

            var procedimentos = ObterProcedimentos(guia).ToList();
            if (procedimentos.Count == 0)
            {
                resultado.Add(CriarRegistroBase(dadosGuia));
                continue;
            }

            foreach (var procedimento in procedimentos)
            {
                resultado.Add(CriarRegistroComProcedimento(dadosGuia, procedimento));
            }
        }

        return resultado;
    }

    private static DadosGuia ExtrairDadosGuia(XElement guia)
    {
        return new DadosGuia(
            Credencial: BuscarPrimeiroValor(guia, TagsCredencial),
            Nome: BuscarPrimeiroValor(guia, TagsNome),
            Senha: BuscarPrimeiroValor(guia, TagsSenha),
            NumeroGuiaPrestador: BuscarPrimeiroValor(guia, "numeroGuiaPrestador"),
            NumeroGuiaOperadora: BuscarPrimeiroValor(guia, "numeroGuiaOperadora"),
            DataAtendimento: BuscarPrimeiroValor(guia, TagsDataAtendimento),
            ValorInformado: FormatarValor(BuscarPrimeiroValor(guia, TagsValorInformadoGuia)),
            ValorProcessado: FormatarValor(BuscarPrimeiroValor(guia, TagsValorProcessadoGuia)),
            ValorLiberado: FormatarValor(BuscarPrimeiroValor(guia, TagsValorLiberadoGuia)),
            ValorGlosa: FormatarValor(BuscarPrimeiroValor(guia, TagsValorGlosaGuia)),
            SituacaoGuia: BuscarPrimeiroValor(guia, "situacaoGuia"),
            CodigoGlosa: BuscarPrimeiroValor(guia, "codigoGlosa"),
            DescricaoGlosa: BuscarPrimeiroValor(guia, "descricaoGlosa"));
    }

    private static RegistroAnaliseSulamerica CriarRegistroBase(DadosGuia dadosGuia)
    {
        return new RegistroAnaliseSulamerica
        {
            Credencial = dadosGuia.Credencial,
            Nome = dadosGuia.Nome,
            Senha = dadosGuia.Senha,
            NumeroGuiaPrestador = dadosGuia.NumeroGuiaPrestador,
            NumeroGuiaOperadora = dadosGuia.NumeroGuiaOperadora,
            DataAtendimento = dadosGuia.DataAtendimento,
            ValorInformado = dadosGuia.ValorInformado,
            ValorProcessado = dadosGuia.ValorProcessado,
            ValorLiberado = dadosGuia.ValorLiberado,
            ValorGlosa = dadosGuia.ValorGlosa,
            SituacaoGuia = dadosGuia.SituacaoGuia,
            CodigoGlosa = dadosGuia.CodigoGlosa,
            DescricaoGlosa = dadosGuia.DescricaoGlosa
        };
    }

    private static RegistroAnaliseSulamerica CriarRegistroComProcedimento(DadosGuia dadosGuia, XElement procedimento)
    {
        return new RegistroAnaliseSulamerica
        {
            Credencial = dadosGuia.Credencial,
            Nome = dadosGuia.Nome,
            Senha = dadosGuia.Senha,
            NumeroGuiaPrestador = dadosGuia.NumeroGuiaPrestador,
            NumeroGuiaOperadora = dadosGuia.NumeroGuiaOperadora,
            DataAtendimento = dadosGuia.DataAtendimento,
            CodigoProcedimento = BuscarPrimeiroValor(procedimento, TagsCodigoProcedimento),
            DescricaoProcedimento = BuscarPrimeiroValor(procedimento, TagsDescricaoProcedimento),
            Quantidade = BuscarPrimeiroValor(procedimento, TagsQuantidadeProcedimento),
            ValorInformado = FormatarValor(BuscarPrimeiroValor(procedimento, TagsValorInformadoProcedimento)),
            ValorProcessado = dadosGuia.ValorProcessado,
            ValorLiberado = dadosGuia.ValorLiberado,
            ValorGlosa = dadosGuia.ValorGlosa,
            SituacaoGuia = dadosGuia.SituacaoGuia,
            CodigoGlosa = dadosGuia.CodigoGlosa,
            DescricaoGlosa = dadosGuia.DescricaoGlosa
        };
    }

    private static IEnumerable<XElement> ObterGuias(XDocument documento)
    {
        return documento
            .Descendants()
            .Where(static elemento => elemento.Name.LocalName.Contains("guia", StringComparison.OrdinalIgnoreCase))
            .Where(static elemento =>
                elemento.Descendants().Any(filho => NomeEh(filho, "numeroCarteira")) ||
                elemento.Descendants().Any(filho => NomeEh(filho, "nomeBeneficiario")) ||
                elemento.Descendants().Any(filho => NomeEh(filho, "senha")));
    }

    private static IEnumerable<XElement> ObterProcedimentos(XElement guia)
    {
        return guia.Descendants().Where(static elemento => NomeEh(elemento, "procedimentoExecutado"));
    }

    private static bool PassaFiltro(
        CampoFiltroSulamerica campoFiltro,
        HashSet<string> filtros,
        string credencial,
        string nome,
        string senha)
    {
        if (filtros.Count == 0)
        {
            return true;
        }

        var valor = campoFiltro switch
        {
            CampoFiltroSulamerica.Credencial => Normalizar(credencial),
            CampoFiltroSulamerica.Nome => Normalizar(nome),
            CampoFiltroSulamerica.Senha => Normalizar(senha),
            _ => string.Empty
        };

        return filtros.Contains(valor);
    }

    private static string BuscarPrimeiroValor(XElement origem, params string[] nomes)
    {
        foreach (var nome in nomes)
        {
            var encontrado = origem
                .Descendants()
                .FirstOrDefault(elemento => NomeEh(elemento, nome));

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

    private static string Normalizar(string valor)
    {
        return valor.Trim();
    }

    private static string FormatarValor(string valorBruto)
    {
        if (decimal.TryParse(valorBruto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorInvariante))
        {
            return valorInvariante.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        }

        if (decimal.TryParse(valorBruto, NumberStyles.Any, CultureInfo.GetCultureInfo("pt-BR"), out var valorPtBr))
        {
            return valorPtBr.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        }

        return valorBruto;
    }

    private readonly record struct DadosGuia(
        string Credencial,
        string Nome,
        string Senha,
        string NumeroGuiaPrestador,
        string NumeroGuiaOperadora,
        string DataAtendimento,
        string ValorInformado,
        string ValorProcessado,
        string ValorLiberado,
        string ValorGlosa,
        string SituacaoGuia,
        string CodigoGlosa,
        string DescricaoGlosa);
}
