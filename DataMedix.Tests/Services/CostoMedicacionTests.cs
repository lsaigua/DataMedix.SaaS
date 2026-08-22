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

    /// <summary>
    /// Precio de EPO cuando la dosis del día no coincide con una presentación.
    ///
    /// Al concentrar la dosis semanal en pocos días aparecen valores fuera de
    /// la tabla. Antes se buscaba por coincidencia exacta y esas dosis se
    /// cobraban en CERO, que es una pérdida silenciosa de facturación.
    /// </summary>
    public class CostoEpoTests
    {
        // Tabla de precios típica del tenant
        private static readonly Dictionary<decimal, decimal> Precios = new()
        {
            [2000m] = 2.80m,
            [4000m] = 4.30m,
            [6000m] = 6.00m,
        };

        [Fact]
        public void Dosis_concentrada_se_descompone_en_viales_de_la_presentacion_del_paciente()
        {
            // 12.000 UI en un solo día = 3 viales de 4.000 → 3 × $4.30 = $12.90
            var (viales, resto) = CostoMedicacion.DescomponerEpo(12000m, Precios.Keys, 4000m);

            viales.Should().Equal(4000m, 4000m, 4000m);
            resto.Should().Be(0m);
            CostoMedicacion.CostoEpoDia(12000m, Precios, 4000m).Should().Be(12.90m);
        }

        [Fact]
        public void Dosis_repartida_en_dos_dias_se_cobra_por_tabla()
        {
            // 12.000 UI en L y J = 6.000 cada día, que sí está en la tabla
            CostoMedicacion.CostoEpoDia(6000m, Precios, 4000m).Should().Be(6.00m);
        }

        [Fact]
        public void Una_dosis_que_esta_en_la_tabla_no_se_descompone()
        {
            var (viales, _) = CostoMedicacion.DescomponerEpo(4000m, Precios.Keys, 2000m);

            viales.Should().Equal(4000m);
        }

        [Fact]
        public void Sin_referencia_usa_la_descomposicion_con_menos_viales()
        {
            var (viales, resto) = CostoMedicacion.DescomponerEpo(12000m, Precios.Keys);

            viales.Should().Equal(6000m, 6000m);
            resto.Should().Be(0m);
        }

        [Fact]
        public void La_referencia_solo_manda_si_divide_exacto()
        {
            // 10.000 no es múltiplo de 4.000: se cae a la descomposición voraz
            var (viales, resto) = CostoMedicacion.DescomponerEpo(10000m, Precios.Keys, 4000m);

            viales.Sum().Should().Be(10000m);
            resto.Should().Be(0m);
        }

        [Fact]
        public void El_resto_no_cubierto_se_informa_en_vez_de_cobrarse_en_silencio()
        {
            var (viales, resto) = CostoMedicacion.DescomponerEpo(5000m, new[] { 2000m });

            viales.Should().Equal(2000m, 2000m);
            resto.Should().Be(1000m);
        }

        [Fact]
        public void Sin_precios_configurados_no_se_inventa_ninguno()
        {
            CostoMedicacion.CostoEpoDia(12000m, new Dictionary<decimal, decimal>()).Should().Be(0m);
        }

        [Fact]
        public void Ocho_mil_en_un_dia_se_factura_como_dos_viales_de_cuatro_mil()
        {
            // Caso reportado: 9 aplicaciones de 8.000 UI salían a $8.50 cada una
            // (descomposición voraz 6.000 + 2.000 = 76,50 en total) porque la
            // referencia era la dosis del día. Con la referencia correcta —el
            // vial que implica la prescripción semanal— son 2 x 4.000.
            var referencia = DistribucionEpo.PresentacionReferencia(8000);

            var (viales, resto) = CostoMedicacion.DescomponerEpo(8000m, Precios.Keys, referencia);

            viales.Should().Equal(4000m, 4000m);
            resto.Should().Be(0m);
            CostoMedicacion.CostoEpoDia(8000m, Precios, referencia).Should().Be(8.60m);
        }

        [Fact]
        public void El_incidente_reportado_con_los_precios_reales()
        {
            // Tabla real del tenant al momento del reporte
            var reales = new Dictionary<decimal, decimal>
            {
                [2000m] = 2.10m, [4000m] = 4.30m, [6000m] = 6.40m,
            };

            // Lo que se facturaba: voraz 6.000 + 2.000, 9 aplicaciones = $76,50
            CostoMedicacion.CostoEpoDia(8000m, reales).Should().Be(8.50m);
            (9 * 8.50m).Should().Be(76.50m);

            // Lo correcto: 2 viales de 4.000, 9 aplicaciones = $77,40
            var referencia = DistribucionEpo.PresentacionReferencia(8000);
            CostoMedicacion.CostoEpoDia(8000m, reales, referencia).Should().Be(8.60m);
            (9 * 8.60m).Should().Be(77.40m);
        }
    }

    /// <summary>
    /// Comprobación de extremo a extremo del caso reportado: 8.000 UI en una
    /// sola aplicación deben facturarse con el precio de la presentación de
    /// 4.000, y el precio unitario mostrado tiene que cuadrar con el costo.
    /// </summary>
    public class OchoMilUnidadesTests
    {
        private static readonly Dictionary<decimal, decimal> Precios = new()
        {
            [2000m] = 2.10m, [4000m] = 4.30m, [6000m] = 6.40m,
        };

        private static decimal CostoAplicacion(decimal dosisDia, decimal semanal) =>
            CostoMedicacion.CostoEpoDia(
                dosisDia, Precios, DistribucionEpo.PresentacionReferencia(semanal));

        [Fact]
        public void Ocho_mil_toma_el_precio_de_la_presentacion_de_cuatro_mil()
        {
            DistribucionEpo.PresentacionReferencia(8000).Should().Be(4000m);
            CostoAplicacion(8000m, 8000m).Should().Be(2 * Precios[4000m]);
        }

        [Fact]
        public void Las_nueve_aplicaciones_del_reporte_cuadran()
        {
            const int aplicaciones = 9;
            var costoUnitario = CostoAplicacion(8000m, 8000m);
            var costoTotal    = aplicaciones * costoUnitario;

            costoUnitario.Should().Be(8.60m);
            costoTotal.Should().Be(77.40m);

            // El precio que muestra la tabla es Costo / Aplicaciones: no puede
            // salir en cero ni contradecir al costo, que era el sintoma original
            var precioMostrado = Math.Round(costoTotal / aplicaciones, 2);
            precioMostrado.Should().Be(costoUnitario);
            precioMostrado.Should().NotBe(0m);
        }

        [Theory]
        // Si la dosis del día EXISTE como presentación se usa un solo vial de
        // esa: nadie pone tres viales de 2.000 teniendo uno de 6.000.
        [InlineData(2000,  2000, 1)]
        [InlineData(4000,  4000, 1)]
        [InlineData(6000,  6000, 1)]
        // Fuera de la tabla, se descompone con la presentación del paciente
        [InlineData(8000,  8000, 2)]   // 2 viales de 4.000
        [InlineData(12000, 12000, 3)]  // 3 viales de 4.000
        [InlineData(18000, 18000, 3)]  // 3 viales de 6.000
        public void Toda_la_dosis_semanal_concentrada_usa_su_propia_presentacion(
            int dosisDia, int semanal, int vialesEsperados)
        {
            var referencia = DistribucionEpo.PresentacionReferencia(semanal);
            var (viales, resto) = CostoMedicacion.DescomponerEpo(dosisDia, Precios.Keys, referencia);

            viales.Should().HaveCount(vialesEsperados);
            viales.Sum().Should().Be(dosisDia);
            resto.Should().Be(0m);
        }
    }
}
