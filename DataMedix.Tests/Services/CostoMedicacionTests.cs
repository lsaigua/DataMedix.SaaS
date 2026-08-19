using DataMedix.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DataMedix.Tests.Services
{
    /// <summary>
    /// Costo del hierro IV. El precio se configura por AMPOLLA de 100 mg, no por
    /// miligramo: tratarlo como precio por mg multiplicaba el cobro por 100.
    /// </summary>
    public class CostoMedicacionTests
    {
        [Theory]
        [InlineData(100, 1)]
        [InlineData(200, 2)]
        [InlineData(400, 4)]
        [InlineData(1000, 10)]
        public void CostoHierro_cobra_por_ampolla_no_por_miligramo(decimal mg, decimal esperado)
        {
            var costo = CostoMedicacion.CostoHierro(mg, precioAmpolla: 1.00m);

            costo.Should().Be(esperado);
        }

        [Fact]
        public void CostoHierro_de_400mg_no_son_400_dolares()
        {
            // El caso reportado: 400 mg con la ampolla a $1 son $4, no $400
            var costo = CostoMedicacion.CostoHierro(400m, 1.00m);

            costo.Should().Be(4m);
            costo.Should().NotBe(400m);
        }

        [Fact]
        public void CostoHierro_aplica_el_precio_configurado_de_la_ampolla()
        {
            CostoMedicacion.CostoHierro(600m, 12.50m).Should().Be(75m);   // 6 ampollas
        }

        [Fact]
        public void CostoHierro_cobra_proporcional_la_ampolla_incompleta()
        {
            // El programador de hierro puede repartir dosis que no son múltiplos
            // exactos de 100 mg; se cobra la fracción, no la ampolla completa.
            CostoMedicacion.CostoHierro(250m, 1.00m).Should().Be(2.5m);
            CostoMedicacion.AmpollasHierro(250m).Should().Be(2.5m);
        }

        [Fact]
        public void CostoHierro_sin_precio_configurado_es_cero()
        {
            CostoMedicacion.CostoHierro(400m, 0m).Should().Be(0m);
        }
    }
}
