using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Windows.Data;

namespace AnaliseDemonstrativoTISS.Operadoras;

public static class UtilitariosDeAnalise
{
    private static readonly CultureInfo CulturaPtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Encoding EncodingFallback = Encoding.Latin1;

    public static HashSet<string> CriarFiltros(IEnumerable<string>? valoresFiltro)
    {
        return (valoresFiltro ?? [])
            .Select(Normalizar)
            .Where(static valor => !string.IsNullOrWhiteSpace(valor))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool PassaFiltro(HashSet<string> filtros, string valorCampo)
    {
        if (filtros.Count == 0)
        {
            return true;
        }

        return filtros.Contains(Normalizar(valorCampo));
    }

    public static string Normalizar(string valor)
    {
        return valor.Trim();
    }

    public static string FormatarValorMonetario(string valorBruto)
    {
        var valorNormalizado = valorBruto.Trim();
        if (string.IsNullOrWhiteSpace(valorNormalizado))
        {
            return valorBruto;
        }

        var possuiVirgula = valorNormalizado.Contains(',', StringComparison.Ordinal);
        var possuiPonto = valorNormalizado.Contains('.', StringComparison.Ordinal);

        if (possuiVirgula && !possuiPonto &&
            decimal.TryParse(valorNormalizado, NumberStyles.Any, CulturaPtBr, out var valorSomenteVirgula))
        {
            return valorSomenteVirgula.ToString("N2", CulturaPtBr);
        }

        if (possuiPonto && !possuiVirgula &&
            decimal.TryParse(valorNormalizado, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorSomentePonto))
        {
            return valorSomentePonto.ToString("N2", CulturaPtBr);
        }

        if (possuiVirgula && possuiPonto)
        {
            var ultimoIndiceVirgula = valorNormalizado.LastIndexOf(',');
            var ultimoIndicePonto = valorNormalizado.LastIndexOf('.');

            if (ultimoIndiceVirgula > ultimoIndicePonto &&
                decimal.TryParse(valorNormalizado, NumberStyles.Any, CulturaPtBr, out var valorPtBrComAmbos))
            {
                return valorPtBrComAmbos.ToString("N2", CulturaPtBr);
            }

            if (ultimoIndicePonto > ultimoIndiceVirgula &&
                decimal.TryParse(valorNormalizado, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorInvarianteComAmbos))
            {
                return valorInvarianteComAmbos.ToString("N2", CulturaPtBr);
            }
        }

        if (decimal.TryParse(valorNormalizado, NumberStyles.Any, CulturaPtBr, out var valorPtBr))
        {
            return valorPtBr.ToString("N2", CulturaPtBr);
        }

        if (decimal.TryParse(valorNormalizado, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorInvariante))
        {
            return valorInvariante.ToString("N2", CulturaPtBr);
        }

        return valorBruto;
    }

    public static XDocument CarregarDocumentoXml(string caminhoXml)
    {
        try
        {
            return XDocument.Load(caminhoXml, LoadOptions.None);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or XmlException)
        {
            using var stream = File.OpenRead(caminhoXml);
            using var streamReader = new StreamReader(stream, EncodingFallback, detectEncodingFromByteOrderMarks: true);
            using var xmlReader = XmlReader.Create(streamReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            return XDocument.Load(xmlReader, LoadOptions.None);
        }
    }
}

public sealed class OneBasedIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int index ? (index + 1).ToString(culture) : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
