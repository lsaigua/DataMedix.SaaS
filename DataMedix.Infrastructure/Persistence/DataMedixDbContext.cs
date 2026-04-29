using Microsoft.EntityFrameworkCore;
using DataMedix.Domain.Entities;

namespace DataMedix.Infrastructure.Persistence
{
    public class DataMedixDbContext : DbContext
    {
        public DataMedixDbContext(DbContextOptions<DataMedixDbContext> options) : base(options) { }

        // Seguridad
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Rol> Roles => Set<Rol>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<UsuarioRol> UsuariosRoles => Set<UsuarioRol>();

        // Clínico
        public DbSet<Paciente> Pacientes => Set<Paciente>();
        public DbSet<ParametroClinico> ParametrosClinicos => Set<ParametroClinico>();
        public DbSet<AliasParametro> AliasParametros => Set<AliasParametro>();

        // Importación
        public DbSet<LoteImportacion> LotesImportacion => Set<LoteImportacion>();
        public DbSet<ImportacionDetalle> ImportacionDetalles => Set<ImportacionDetalle>();
        public DbSet<ImportacionError> ImportacionErrores => Set<ImportacionError>();
        public DbSet<ResultadoLaboratorio> ResultadosLaboratorio => Set<ResultadoLaboratorio>();

        // Snapshot mensual
        public DbSet<SnapshotMensual> SnapshotsMensuales => Set<SnapshotMensual>();
        public DbSet<SnapshotMensualDetalle> SnapshotsMensualesDetalle => Set<SnapshotMensualDetalle>();

        // Prescripción
        public DbSet<RangoPrescriba> RangosPrescriba => Set<RangoPrescriba>();
        public DbSet<PrescripcionSugerida> PrescripcionesSugeridas => Set<PrescripcionSugerida>();
        public DbSet<PrescripcionFinal> PrescripcionesFinales => Set<PrescripcionFinal>();

        // Auditoría
        public DbSet<AuditoriaLog> AuditoriaLogs => Set<AuditoriaLog>();

        protected override void OnModelCreating(ModelBuilder m)
        {
            base.OnModelCreating(m);

            // ========================
            // TENANT
            // ========================
            m.Entity<Tenant>(e =>
            {
                e.ToTable("tenant");
                e.HasKey(t => t.Id);
                e.Property(t => t.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(t => t.Codigo).HasColumnName("codigo").HasMaxLength(20);
                e.Property(t => t.Nombre).HasColumnName("nombre").HasMaxLength(200);
                e.Property(t => t.Ruc).HasColumnName("ruc").HasMaxLength(20);
                e.Property(t => t.Subdomain).HasColumnName("subdomain").HasMaxLength(50).IsRequired();
                e.Property(t => t.LogoUrl).HasColumnName("logo_url");
                e.Property(t => t.EmailContacto).HasColumnName("email_contacto").HasMaxLength(200);
                e.Property(t => t.Telefono).HasColumnName("telefono").HasMaxLength(20);
                e.Property(t => t.Direccion).HasColumnName("direccion");
                e.Property(t => t.Ciudad).HasColumnName("ciudad").HasMaxLength(100);
                e.Property(t => t.Pais).HasColumnName("pais").HasMaxLength(100);
                e.Property(t => t.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(t => t.CreatedAt).HasColumnName("created_at");
                e.Property(t => t.UpdatedAt).HasColumnName("updated_at");
                e.Property(t => t.DeletedAt).HasColumnName("deleted_at");
                e.HasIndex(t => t.Subdomain).IsUnique();
            });

            // ========================
            // ROL
            // ========================
            m.Entity<Rol>(e =>
            {
                e.ToTable("rol");
                e.HasKey(r => r.Id);
                e.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(50).IsRequired();
                e.Property(r => r.Descripcion).HasColumnName("descripcion");
                e.Property(r => r.EsGlobal).HasColumnName("es_global").HasDefaultValue(false);
                e.Property(r => r.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(r => r.CreatedAt).HasColumnName("created_at");
            });

            // ========================
            // USUARIO
            // ========================
            m.Entity<Usuario>(e =>
            {
                e.ToTable("usuario");
                e.HasKey(u => u.Id);
                e.Property(u => u.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(u => u.TenantId).HasColumnName("tenant_id");
                e.Property(u => u.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
                e.Property(u => u.Identificacion).HasColumnName("identificacion").HasMaxLength(20).IsRequired();
                e.Property(u => u.PrimerNombre).HasColumnName("primer_nombre").HasMaxLength(150).IsRequired();
                e.Property(u => u.SegundoNombre).HasColumnName("segundo_nombre").HasMaxLength(150);
                e.Property(u => u.PrimerApellido).HasColumnName("primer_apellido").HasMaxLength(150).IsRequired();
                e.Property(u => u.SegundoApellido).HasColumnName("segundo_apellido").HasMaxLength(150);
                e.Property(u => u.Email).HasColumnName("email").HasMaxLength(200);
                e.Property(u => u.Telefono).HasColumnName("telefono").HasMaxLength(20);
                e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
                e.Property(u => u.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(u => u.UltimoAcceso).HasColumnName("ultimo_acceso");
                e.Property(u => u.CreatedAt).HasColumnName("created_at");
                e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
                e.Property(u => u.CreatedBy).HasColumnName("created_by");
                e.Ignore(u => u.NombreCompleto);
                e.HasIndex(u => u.Email).IsUnique();
                e.HasIndex(u => u.TenantId);
                e.HasOne(u => u.Tenant).WithMany(t => t.Usuarios)
                    .HasForeignKey(u => u.TenantId).IsRequired(false);
            });

            // ========================
            // USUARIO_ROL
            // ========================
            m.Entity<UsuarioRol>(e =>
            {
                e.ToTable("usuario_rol");
                e.HasKey(ur => ur.Id);
                e.Property(ur => ur.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(ur => ur.UsuarioId).HasColumnName("usuario_id").IsRequired();
                e.Property(ur => ur.RolId).HasColumnName("rol_id").IsRequired();
                e.Property(ur => ur.TenantId).HasColumnName("tenant_id");
                e.Property(ur => ur.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(ur => ur.CreatedAt).HasColumnName("created_at");
                e.HasOne(ur => ur.Usuario).WithMany(u => u.Roles).HasForeignKey(ur => ur.UsuarioId);
                e.HasOne(ur => ur.Rol).WithMany(r => r.UsuariosRoles)
                    .HasForeignKey(ur => ur.RolId).IsRequired(false);
            });

            // ========================
            // PACIENTE
            // ========================
            m.Entity<Paciente>(e =>
            {
                e.ToTable("paciente");
                e.HasKey(p => p.Id);
                e.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(p => p.TenantId).HasColumnName("tenant_id");
                e.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(50);
                e.Property(p => p.Identificacion).HasColumnName("identificacion").HasMaxLength(20).IsRequired();
                e.Property(p => p.PrimerNombre).HasColumnName("primer_nombre").HasMaxLength(150).IsRequired();
                e.Property(p => p.SegundoNombre).HasColumnName("segundo_nombre").HasMaxLength(150);
                e.Property(p => p.PrimerApellido).HasColumnName("primer_apellido").HasMaxLength(150).IsRequired();
                e.Property(p => p.SegundoApellido).HasColumnName("segundo_apellido").HasMaxLength(150);
                e.Property(p => p.FechaNacimiento).HasColumnName("fecha_nacimiento").HasColumnType("date");
                e.Property(p => p.Genero).HasColumnName("genero").HasMaxLength(1);
                e.Property(p => p.Email).HasColumnName("email").HasMaxLength(200);
                e.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20);
                e.Property(p => p.PlanSalud).HasColumnName("plan_salud").HasMaxLength(200);
                e.Property(p => p.TipoAtencion).HasColumnName("tipo_atencion").HasMaxLength(200);
                e.Property(p => p.FechaIngreso).HasColumnName("fecha_ingreso").HasColumnType("date");
                e.Property(p => p.MedicoResponsable).HasColumnName("medico_responsable").HasMaxLength(300);
                e.Property(p => p.Observaciones).HasColumnName("observaciones");
                e.Property(p => p.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(p => p.CreatedAt).HasColumnName("created_at");
                e.Property(p => p.UpdatedAt).HasColumnName("updated_at");
                e.Property(p => p.CreatedBy).HasColumnName("created_by");
                // nombre_completo es GENERATED ALWAYS AS en Postgres; C# la computa también → ignorar
                e.Ignore(p => p.NombreCompleto);
                e.Ignore(p => p.MesesEnDialisis);
                e.HasIndex(p => p.TenantId);
                e.HasOne(p => p.Tenant).WithMany(t => t.Pacientes).HasForeignKey(p => p.TenantId).IsRequired(false);
            });

            // ========================
            // PARAMETRO CLINICO
            // ========================
            m.Entity<ParametroClinico>(e =>
            {
                e.ToTable("parametro_clinico");
                e.HasKey(p => p.Id);
                e.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(p => p.Codigo).HasColumnName("codigo").HasMaxLength(50).IsRequired();
                e.Property(p => p.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
                e.Property(p => p.Descripcion).HasColumnName("descripcion");
                e.Property(p => p.UnidadMedidaDefault).HasColumnName("unidad_medida_default").HasMaxLength(50);
                e.Property(p => p.TipoDato).HasColumnName("tipo_dato").HasMaxLength(20).HasDefaultValue("numerico");
                e.Property(p => p.ValorMinReferencia).HasColumnName("valor_min_referencia").HasColumnType("decimal(12,4)");
                e.Property(p => p.ValorMaxReferencia).HasColumnName("valor_max_referencia").HasColumnType("decimal(12,4)");
                e.Property(p => p.EsParametroClave).HasColumnName("es_parametro_clave").HasDefaultValue(false);
                e.Property(p => p.OrdenVisualizacion).HasColumnName("orden_visualizacion").HasDefaultValue(99);
                e.Property(p => p.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasIndex(p => p.Codigo).IsUnique();
            });

            // ========================
            // ALIAS PARAMETRO
            // ========================
            m.Entity<AliasParametro>(e =>
            {
                e.ToTable("alias_parametro");
                e.HasKey(a => a.Id);
                e.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(a => a.ParametroClinicoId).HasColumnName("parametro_clinico_id").IsRequired();
                e.Property(a => a.TenantId).HasColumnName("tenant_id");
                e.Property(a => a.Alias).HasColumnName("alias").IsRequired();
                e.Property(a => a.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasOne(a => a.ParametroClinico).WithMany(p => p.Aliases).HasForeignKey(a => a.ParametroClinicoId);
            });

            // ========================
            // LOTE IMPORTACION
            // ========================
            m.Entity<LoteImportacion>(e =>
            {
                e.ToTable("lote_importacion");
                e.HasKey(l => l.Id);
                e.Property(l => l.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(l => l.NombreArchivo).HasColumnName("nombre_archivo").HasMaxLength(500).IsRequired();
                e.Property(l => l.NombreArchivoOriginal).HasColumnName("nombre_archivo_original").HasMaxLength(500).IsRequired();
                e.Property(l => l.StoragePath).HasColumnName("storage_path");
                e.Property(l => l.PeriodoAnio).HasColumnName("periodo_anio").IsRequired();
                e.Property(l => l.PeriodoMes).HasColumnName("periodo_mes").IsRequired();
                e.Property(l => l.PeriodDate).HasColumnName("period_date").HasColumnType("date").IsRequired();
                e.Property(l => l.TotalFilas).HasColumnName("total_filas").HasDefaultValue(0);
                e.Property(l => l.FilasValidas).HasColumnName("filas_validas").HasDefaultValue(0);
                e.Property(l => l.FilasError).HasColumnName("filas_error").HasDefaultValue(0);
                e.Property(l => l.FilasDuplicadas).HasColumnName("filas_duplicadas").HasDefaultValue(0);
                e.Property(l => l.Estado).HasColumnName("estado").HasMaxLength(50).HasDefaultValue("PENDIENTE");
                e.Property(l => l.MensajeError).HasColumnName("mensaje_error");
                e.Property(l => l.FechaInicioProceso).HasColumnName("fecha_inicio_proceso");
                e.Property(l => l.FechaFinProceso).HasColumnName("fecha_fin_proceso");
                e.Property(l => l.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(l => l.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.Property(l => l.CreatedBy).HasColumnName("created_by");
                e.HasIndex(l => new { l.TenantId, l.PeriodDate });
                e.HasOne(l => l.Tenant).WithMany().HasForeignKey(l => l.TenantId);
            });

            // ========================
            // IMPORTACION DETALLE (STAGING)
            // ========================
            m.Entity<ImportacionDetalle>(e =>
            {
                e.ToTable("importacion_detalle");
                e.HasKey(d => d.Id);
                e.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(d => d.LoteId).HasColumnName("lote_id").IsRequired();
                e.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(d => d.NumeroFila).HasColumnName("numero_fila").IsRequired();
                e.Property(d => d.FechaOrdenRaw).HasColumnName("fecha_orden_raw").HasMaxLength(50);
                e.Property(d => d.PlanSaludRaw).HasColumnName("plan_salud_raw").HasMaxLength(300);
                e.Property(d => d.TipoAtencionRaw).HasColumnName("tipo_atencion_raw").HasMaxLength(300);
                e.Property(d => d.IdentificacionRaw).HasColumnName("identificacion_raw").HasMaxLength(50);
                e.Property(d => d.PacienteRaw).HasColumnName("paciente_raw").HasMaxLength(500);
                e.Property(d => d.ExamenRaw).HasColumnName("examen_raw").HasMaxLength(500);
                e.Property(d => d.ParametroRaw).HasColumnName("parametro_raw").HasMaxLength(500);
                e.Property(d => d.ResultadoRaw).HasColumnName("resultado_raw").HasMaxLength(300);
                e.Property(d => d.UnidadMedidaRaw).HasColumnName("unidad_medida_raw").HasMaxLength(100);
                e.Property(d => d.PeriodDate).HasColumnName("period_date").HasColumnType("date");
                e.Property(d => d.PacienteId).HasColumnName("paciente_id");
                e.Property(d => d.ParametroClinicoId).HasColumnName("parametro_clinico_id");
                e.Property(d => d.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("PENDIENTE");
                e.Property(d => d.CreatedAt).HasColumnName("created_at");
                e.HasIndex(d => d.LoteId);
                e.HasOne(d => d.Lote).WithMany(l => l.Detalles).HasForeignKey(d => d.LoteId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.Paciente).WithMany().HasForeignKey(d => d.PacienteId).IsRequired(false);
                e.HasOne(d => d.ParametroClinico).WithMany().HasForeignKey(d => d.ParametroClinicoId).IsRequired(false);
            });

            // ========================
            // IMPORTACION ERROR
            // ========================
            m.Entity<ImportacionError>(e =>
            {
                e.ToTable("importacion_error");
                e.HasKey(ie => ie.Id);
                e.Property(ie => ie.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(ie => ie.LoteId).HasColumnName("lote_id").IsRequired();
                e.Property(ie => ie.ImportacionDetalleId).HasColumnName("importacion_detalle_id");
                e.Property(ie => ie.NumeroFila).HasColumnName("numero_fila");
                e.Property(ie => ie.Campo).HasColumnName("campo").HasMaxLength(100);
                e.Property(ie => ie.TipoError).HasColumnName("tipo_error").HasMaxLength(50);
                e.Property(ie => ie.Mensaje).HasColumnName("mensaje").IsRequired();
                e.Property(ie => ie.ValorRecibido).HasColumnName("valor_recibido");
                e.Property(ie => ie.EsIgnorado).HasColumnName("es_ignorado").HasDefaultValue(false);
                e.Property(ie => ie.CreatedAt).HasColumnName("created_at");
                e.HasOne(ie => ie.Lote).WithMany(l => l.Errores).HasForeignKey(ie => ie.LoteId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(ie => ie.Detalle).WithMany().HasForeignKey(ie => ie.ImportacionDetalleId).IsRequired(false);
            });

            // ========================
            // RESULTADO LABORATORIO
            // ========================
            m.Entity<ResultadoLaboratorio>(e =>
            {
                e.ToTable("resultado_laboratorio");
                e.HasKey(r => r.Id);
                e.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(r => r.PacienteId).HasColumnName("paciente_id").IsRequired();
                e.Property(r => r.LoteId).HasColumnName("lote_id").IsRequired();
                e.Property(r => r.ParametroClinicoId).HasColumnName("parametro_clinico_id");
                e.Property(r => r.PeriodDate).HasColumnName("period_date").HasColumnType("date").IsRequired();
                e.Property(r => r.PeriodoAnio).HasColumnName("periodo_anio").IsRequired();
                e.Property(r => r.PeriodoMes).HasColumnName("periodo_mes").IsRequired();
                e.Property(r => r.PlanSalud).HasColumnName("plan_salud").HasMaxLength(200);
                e.Property(r => r.TipoAtencion).HasColumnName("tipo_atencion").HasMaxLength(200);
                e.Property(r => r.FechaOrden).HasColumnName("fecha_orden").HasColumnType("date");
                e.Property(r => r.ExamenRaw).HasColumnName("examen_raw").HasMaxLength(300);
                e.Property(r => r.ParametroRaw).HasColumnName("parametro_raw").HasMaxLength(300);
                e.Property(r => r.ResultadoTexto).HasColumnName("resultado_texto").HasMaxLength(300).IsRequired();
                e.Property(r => r.ValorNumerico).HasColumnName("valor_numerico").HasColumnType("decimal(14,4)");
                e.Property(r => r.UnidadMedida).HasColumnName("unidad_medida").HasMaxLength(100);
                e.Property(r => r.ValorMinReferencia).HasColumnName("valor_min_referencia").HasColumnType("decimal(10,4)");
                e.Property(r => r.ValorMaxReferencia).HasColumnName("valor_max_referencia").HasColumnType("decimal(10,4)");
                e.Property(r => r.EsPatologico).HasColumnName("es_patologico").HasDefaultValue(false);
                e.Property(r => r.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(r => r.CreatedAt).HasColumnName("created_at");
                e.Property(r => r.CreatedBy).HasColumnName("created_by");
                e.HasIndex(r => new { r.TenantId, r.PacienteId, r.PeriodDate });
                e.HasIndex(r => new { r.TenantId, r.PeriodDate });
                e.HasOne(r => r.Paciente).WithMany(p => p.Resultados).HasForeignKey(r => r.PacienteId);
                e.HasOne(r => r.Lote).WithMany(l => l.Resultados).HasForeignKey(r => r.LoteId);
                e.HasOne(r => r.ParametroClinico).WithMany(p => p.Resultados).HasForeignKey(r => r.ParametroClinicoId).IsRequired(false);
            });

            // ========================
            // SNAPSHOT MENSUAL
            // ========================
            m.Entity<SnapshotMensual>(e =>
            {
                e.ToTable("snapshot_mensual");
                e.HasKey(s => s.Id);
                e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(s => s.PacienteId).HasColumnName("paciente_id").IsRequired();
                e.Property(s => s.PeriodDate).HasColumnName("period_date").HasColumnType("date").IsRequired();
                e.Property(s => s.PeriodoAnio).HasColumnName("periodo_anio").IsRequired();
                e.Property(s => s.PeriodoMes).HasColumnName("periodo_mes").IsRequired();
                e.Property(s => s.LoteId).HasColumnName("lote_id");
                e.Property(s => s.PlanSalud).HasColumnName("plan_salud").HasMaxLength(200);
                e.Property(s => s.TipoAtencion).HasColumnName("tipo_atencion").HasMaxLength(200);
                e.Property(s => s.HbValor).HasColumnName("hb_valor").HasColumnType("decimal(8,4)");
                e.Property(s => s.HbUnidad).HasColumnName("hb_unidad").HasMaxLength(20);
                e.Property(s => s.HierroValor).HasColumnName("hierro_valor").HasColumnType("decimal(8,4)");
                e.Property(s => s.HierroUnidad).HasColumnName("hierro_unidad").HasMaxLength(20);
                e.Property(s => s.FerritinaValor).HasColumnName("ferritina_valor").HasColumnType("decimal(8,4)");
                e.Property(s => s.FerritinaUnidad).HasColumnName("ferritina_unidad").HasMaxLength(20);
                e.Property(s => s.SaturacionValor).HasColumnName("saturacion_valor").HasColumnType("decimal(8,4)");
                e.Property(s => s.SaturacionUnidad).HasColumnName("saturacion_unidad").HasMaxLength(20);
                e.Property(s => s.TieneDatosCompletos).HasColumnName("tiene_datos_completos").HasDefaultValue(false);
                e.Property(s => s.EsDatosPeriodoAnterior).HasColumnName("es_datos_periodo_anterior").HasDefaultValue(false);
                e.Property(s => s.PeriodDateReal).HasColumnName("period_date_real").HasColumnType("date");
                e.Property(s => s.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.Property(s => s.UpdatedAt).HasColumnName("updated_at");
                e.HasIndex(s => new { s.TenantId, s.PacienteId, s.PeriodDate }).IsUnique();
                e.HasOne(s => s.Paciente).WithMany(p => p.Snapshots).HasForeignKey(s => s.PacienteId);
                e.HasOne(s => s.Lote).WithMany().HasForeignKey(s => s.LoteId).IsRequired(false);
                e.HasOne(s => s.PrescripcionSugerida).WithOne(ps => ps.Snapshot)
                    .HasForeignKey<PrescripcionSugerida>(ps => ps.SnapshotId).IsRequired(false);
            });

            // ========================
            // SNAPSHOT MENSUAL DETALLE
            // ========================
            m.Entity<SnapshotMensualDetalle>(e =>
            {
                e.ToTable("snapshot_mensual_detalle");
                e.HasKey(d => d.Id);
                e.Property(d => d.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(d => d.SnapshotId).HasColumnName("snapshot_id").IsRequired();
                e.Property(d => d.ParametroClinicoId).HasColumnName("parametro_clinico_id");
                e.Property(d => d.ParametroNombre).HasColumnName("parametro_nombre").HasMaxLength(200);
                e.Property(d => d.ValorTexto).HasColumnName("valor_texto").HasMaxLength(300);
                e.Property(d => d.ValorNumerico).HasColumnName("valor_numerico").HasColumnType("decimal(14,4)");
                e.Property(d => d.UnidadMedida).HasColumnName("unidad_medida").HasMaxLength(100);
                e.Property(d => d.EsPatologico).HasColumnName("es_patologico").HasDefaultValue(false);
                e.Property(d => d.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasOne(d => d.Snapshot).WithMany(s => s.Detalles).HasForeignKey(d => d.SnapshotId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.ParametroClinico).WithMany().HasForeignKey(d => d.ParametroClinicoId).IsRequired(false);
            });

            // ========================
            // RANGO PRESCRIBA
            // ========================
            m.Entity<RangoPrescriba>(e =>
            {
                e.ToTable("rango_prescriba");
                e.HasKey(r => r.Id);
                e.Property(r => r.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(r => r.TenantId).HasColumnName("tenant_id");
                e.Property(r => r.ParametroClinicoId).HasColumnName("parametro_clinico_id").IsRequired();
                e.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(300).IsRequired();
                e.Property(r => r.Descripcion).HasColumnName("descripcion");
                e.Property(r => r.ValorMinimo).HasColumnName("valor_minimo").HasColumnType("decimal(12,4)");
                e.Property(r => r.ValorMaximo).HasColumnName("valor_maximo").HasColumnType("decimal(12,4)");
                e.Property(r => r.Accion).HasColumnName("accion").HasMaxLength(50).IsRequired();
                e.Property(r => r.Medicamento).HasColumnName("medicamento").HasMaxLength(100);
                e.Property(r => r.DosisSugerida).HasColumnName("dosis_sugerida");
                e.Property(r => r.AjustePorcentaje).HasColumnName("ajuste_porcentaje").HasColumnType("decimal(7,2)");
                e.Property(r => r.Observacion).HasColumnName("observacion");
                e.Property(r => r.Orden).HasColumnName("orden").HasDefaultValue(99);
                e.Property(r => r.VigenteDesdé).HasColumnName("vigente_desde").HasColumnType("date");
                e.Property(r => r.VigenteHasta).HasColumnName("vigente_hasta").HasColumnType("date");
                e.Property(r => r.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(r => r.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.Property(r => r.UpdatedAt).HasColumnName("updated_at");
                e.Property(r => r.CreatedBy).HasColumnName("created_by");
                e.HasOne(r => r.ParametroClinico).WithMany(p => p.Rangos).HasForeignKey(r => r.ParametroClinicoId);
            });

            // ========================
            // PRESCRIPCION SUGERIDA
            // ========================
            m.Entity<PrescripcionSugerida>(e =>
            {
                e.ToTable("prescripcion_sugerida");
                e.HasKey(ps => ps.Id);
                e.Property(ps => ps.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(ps => ps.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(ps => ps.PacienteId).HasColumnName("paciente_id").IsRequired();
                e.Property(ps => ps.SnapshotId).HasColumnName("snapshot_id");
                e.Property(ps => ps.PeriodDate).HasColumnName("period_date").HasColumnType("date").IsRequired();
                e.Property(ps => ps.EpoAccion).HasColumnName("epo_accion").HasMaxLength(50);
                e.Property(ps => ps.EpoDosisSugerida).HasColumnName("epo_dosis_sugerida");
                e.Property(ps => ps.EpoObservacion).HasColumnName("epo_observacion");
                e.Property(ps => ps.EpoRangoId).HasColumnName("epo_rango_id");
                e.Property(ps => ps.HierroAccion).HasColumnName("hierro_accion").HasMaxLength(50);
                e.Property(ps => ps.HierroDosisSugerida).HasColumnName("hierro_dosis_sugerida");
                e.Property(ps => ps.HierroObservacion).HasColumnName("hierro_observacion");
                e.Property(ps => ps.HierroRangoId).HasColumnName("hierro_rango_id");
                e.Property(ps => ps.ObservacionesGenerales).HasColumnName("observaciones_generales");
                e.Property(ps => ps.Estado).HasColumnName("estado").HasMaxLength(50).HasDefaultValue("PENDIENTE");
                e.Property(ps => ps.RevisadoPor).HasColumnName("revisado_por");
                e.Property(ps => ps.RevisadoAt).HasColumnName("revisado_at");
                e.Property(ps => ps.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(ps => ps.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasIndex(ps => new { ps.TenantId, ps.PacienteId, ps.PeriodDate }).IsUnique();
                e.HasOne(ps => ps.Paciente).WithMany().HasForeignKey(ps => ps.PacienteId);
                e.HasOne(ps => ps.EpoRango).WithMany().HasForeignKey(ps => ps.EpoRangoId).IsRequired(false);
                e.HasOne(ps => ps.HierroRango).WithMany().HasForeignKey(ps => ps.HierroRangoId).IsRequired(false);
                e.HasOne(ps => ps.PrescripcionFinal).WithOne(pf => pf.PrescripcionSugerida)
                    .HasForeignKey<PrescripcionFinal>(pf => pf.PrescripcionSugeridaId).IsRequired(false);
            });

            // ========================
            // PRESCRIPCION FINAL
            // ========================
            m.Entity<PrescripcionFinal>(e =>
            {
                e.ToTable("prescripcion_final");
                e.HasKey(pf => pf.Id);
                e.Property(pf => pf.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(pf => pf.TenantId).HasColumnName("tenant_id").IsRequired();
                e.Property(pf => pf.PacienteId).HasColumnName("paciente_id").IsRequired();
                e.Property(pf => pf.PrescripcionSugeridaId).HasColumnName("prescripcion_sugerida_id");
                e.Property(pf => pf.MedicoId).HasColumnName("medico_id").IsRequired();
                e.Property(pf => pf.PeriodDate).HasColumnName("period_date").HasColumnType("date").IsRequired();
                e.Property(pf => pf.EpoPrescrito).HasColumnName("epo_prescrito").HasDefaultValue(false);
                e.Property(pf => pf.EpoDosis).HasColumnName("epo_dosis");
                e.Property(pf => pf.EpoFrecuencia).HasColumnName("epo_frecuencia");
                e.Property(pf => pf.EpoObservacion).HasColumnName("epo_observacion");
                e.Property(pf => pf.HierroPrescrito).HasColumnName("hierro_prescrito").HasDefaultValue(false);
                e.Property(pf => pf.HierroDosis).HasColumnName("hierro_dosis");
                e.Property(pf => pf.HierroFrecuencia).HasColumnName("hierro_frecuencia");
                e.Property(pf => pf.HierroObservacion).HasColumnName("hierro_observacion");
                e.Property(pf => pf.Observaciones).HasColumnName("observaciones");
                e.Property(pf => pf.Diagnostico).HasColumnName("diagnostico");
                e.Property(pf => pf.Estado).HasColumnName("estado").HasMaxLength(50).HasDefaultValue("ACTIVA");
                e.Property(pf => pf.AprobadoAt).HasColumnName("aprobado_at").HasDefaultValueSql("now()");
                e.Property(pf => pf.Activo).HasColumnName("activo").HasDefaultValue(true);
                e.Property(pf => pf.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.Property(pf => pf.UpdatedAt).HasColumnName("updated_at");
                e.HasOne(pf => pf.Paciente).WithMany().HasForeignKey(pf => pf.PacienteId);
                e.HasOne(pf => pf.Medico).WithMany().HasForeignKey(pf => pf.MedicoId);
            });

            // ========================
            // AUDITORIA LOG
            // ========================
            m.Entity<AuditoriaLog>(e =>
            {
                e.ToTable("auditoria_log");
                e.HasKey(a => a.Id);
                e.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
                e.Property(a => a.TenantId).HasColumnName("tenant_id");
                e.Property(a => a.UsuarioId).HasColumnName("usuario_id");
                e.Property(a => a.Accion).HasColumnName("accion").HasMaxLength(100).IsRequired();
                e.Property(a => a.Entidad).HasColumnName("entidad").HasMaxLength(100);
                e.Property(a => a.EntidadId).HasColumnName("entidad_id");
                e.Property(a => a.Descripcion).HasColumnName("descripcion");
                e.Property(a => a.DatosAnteriores).HasColumnName("datos_anteriores").HasColumnType("jsonb");
                e.Property(a => a.DatosNuevos).HasColumnName("datos_nuevos").HasColumnType("jsonb");
                e.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
                e.Property(a => a.UserAgent).HasColumnName("user_agent");
                e.Property(a => a.Resultado).HasColumnName("resultado").HasMaxLength(20).HasDefaultValue("OK");
                e.Property(a => a.MensajeError).HasColumnName("mensaje_error");
                e.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                e.HasIndex(a => a.TenantId);
                e.HasIndex(a => a.CreatedAt);
            });
        }
    }
}
