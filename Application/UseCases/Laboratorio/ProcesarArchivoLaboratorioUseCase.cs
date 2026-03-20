using DataMedix.Application.DTOs;
using DataMedix.Application.Interfaces;
using DataMedix.Application.Validators;
using DataMedix.Domain.Entities;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DataMedix.Application.UseCases.Laboratorio
{
    public class ProcesarArchivoLaboratorioUseCase
    {
        private readonly IPacienteRepository _pacienteRepo;
        private readonly IOrdenClinicaRepository _ordenRepo;
        private readonly IParametroRepository _parametroRepo;
        private readonly IResultadoRepository _resultadoRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExcelReader _excelReader;

        public ProcesarArchivoLaboratorioUseCase(
            IPacienteRepository pacienteRepo,
            IOrdenClinicaRepository ordenRepo,
            IParametroRepository parametroRepo,
            IResultadoRepository resultadoRepo,
            IUnitOfWork unitOfWork,
            IExcelReader excelReader    )
        {
            _pacienteRepo = pacienteRepo;
            _ordenRepo = ordenRepo;
            _parametroRepo = parametroRepo;
            _resultadoRepo = resultadoRepo;
            _unitOfWork = unitOfWork;
            _excelReader = excelReader;
        }

        public async Task EjecutarAsync( List<LabRowDto> filas)
        {
           // await _unitOfWork.BeginAsync();
            LabImportValidator labImportValidator = new LabImportValidator();
            

            try
            {
                var resultados = new List<ResultadoLaboratorio>();

                foreach (var row in filas)
                {
                    Guid empresaId = Guid.Parse("d8e445fb-b5fb-4416-a39d-b8f00cb10b41");
                    string nombresPaciente = string.Empty;
                    nombresPaciente = row.PrimerNombre + " " + row.PrimerApellido;

                    if (!labImportValidator.CedulaValida(row.Identificacion))
                        continue;

                    var paciente = await _pacienteRepo
                        .GetByIdentificacionAsync(row.Identificacion);

                    if (paciente == null)
                    {
                        paciente = new Paciente(row.Identificacion, nombresPaciente);
                        await _pacienteRepo.AddAsync(paciente);
                    }

                    var orden = await _ordenRepo
                        .GetByPacienteYFechaAsync(paciente.IdPaciente, row.FechaExamen);

                    if (orden == null)
                    {
                        orden = new OrdenClinica(
                            empresaId,
                            paciente.IdPaciente,
                            row.FechaExamen);

                        await _ordenRepo.AddAsync(orden);
                    }

                    var parametro = await _parametroRepo
                        .GetByNombreAsync(empresaId, row.Parametro);

                    if (parametro == null) continue;

                    double? valor = double.TryParse(row.ResultadoTexto,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var n) ? n : null;

                    var resultado = new ResultadoLaboratorio(
                        empresaId,
                        orden.IdOrdenClinica,
                        parametro.IdParametroLaboratorio,
                        row.Examen,
                        row.ResultadoTexto,
                        valor,
                        parametro.ValorMinimo,
                        parametro.ValorMaximo
                    );

                    resultados.Add(resultado);
                }

                await _resultadoRepo.BulkInsertAsync(resultados);

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
        public async Task EjecutarDesdeArchivoAsync(Stream fileStream)
        {
            var filas = await _excelReader.ReadAsync(fileStream);

            await EjecutarAsync(filas);
        }
    }
}
