using Microsoft.EntityFrameworkCore;
using DataMedix.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataMedix.Infrastructure.Persistence
{
    public class DataMedixDbContext : DbContext
    {
        public DataMedixDbContext(DbContextOptions<DataMedixDbContext> options)
            : base(options) { }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Paciente> Pacientes => Set<Paciente>();

        public DbSet<OrdenClinica> OrdenesClinicas => Set<OrdenClinica>();
        public DbSet<ParametroLaboratorio> ParametroLaboratorios => Set<ParametroLaboratorio>();
        public DbSet<ResultadoLaboratorio> ResultadosLaboratorio => Set<ResultadoLaboratorio>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Tenant>(entity =>
            {
                
                entity.HasKey(t => t.id);
               
                entity.Property(t => t.name).IsRequired().HasMaxLength(100);
                entity.Property(t => t.subdomain).IsRequired().HasMaxLength(50);
                entity.Property(t => t.isactive).IsRequired();
            });

            // entidad usuario
            modelBuilder.Entity<Usuario>(entity =>
            {
                // =========================
                // Tabla
                // =========================
                entity.ToTable("usuario");

                // =========================
                // Primary Key
                // =========================
                entity.HasKey(e => e.IdUsuario)
                      .HasName("pk_usuario");

                entity.Property(e => e.IdUsuario)
                      .HasColumnName("id")
                      .HasColumnType("uuid")
                      .HasDefaultValueSql("uuid_generate_v4()")
                      .ValueGeneratedOnAdd();

                // =========================
                // Código de usuario
                // =========================
                entity.Property(e => e.CodigoUsuario)
                      .HasColumnName("codigo")
                      .HasMaxLength(50)
                      .IsRequired();

                // =========================
                // Identificación
                // =========================
                entity.Property(e => e.Identificacion)
                      .HasColumnName("identificacion")
                      .HasMaxLength(15)
                      .IsRequired();

                // =========================
                // Nombres
                // =========================
                entity.Property(e => e.PrimerNombre)
                      .HasColumnName("primernombre")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.SegundoNombre)
                      .HasColumnName("segundonombre")
                      .HasMaxLength(150);

                // =========================
                // Apellidos
                // =========================
                entity.Property(e => e.PrimerApellido)
                      .HasColumnName("primerapellido")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.SegundoApellido)
                      .HasColumnName("segundoapellido")
                      .HasMaxLength(150);

                // =========================
                // Email
                // =========================
                entity.Property(e => e.Email)
                      .HasColumnName("email")
                      .HasMaxLength(56);

                // =========================
                // Teléfono
                // =========================
                entity.Property(e => e.Telefono)
                      .HasColumnName("telefono")
                      .HasMaxLength(15);

                // =========================
                // Tipo de usuario
                // =========================
                entity.Property(e => e.TipoUsuario)
                      .HasColumnName("tipousario") //  respetando el typo en BD
                      .HasMaxLength(15)
                      .IsRequired();

                // =========================
                // Activo
                // =========================
                entity.Property(e => e.Activo)
                      .HasColumnName("activo")
                      .HasDefaultValue(true)
                      .IsRequired();

                // =========================
                // Fecha de creación
                // =========================
                entity.Property(e => e.FechaCreacion)
                      .HasColumnName("fechacreacion")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();

                // =========================
                // Password
                // =========================
                entity.Property(e => e.Password)
                      .HasColumnName("password")
                      .HasMaxLength(255)
                      .IsRequired();

                // =========================
                // Passwordhash
                // =========================
                entity.Property(e => e.PasswordHash)
                      .HasColumnName("PasswordHash")
                      .HasMaxLength(255)
                      .IsRequired();
            });

            // entidad paciente
            modelBuilder.Entity<Paciente>(entity =>
            {
                // =========================
                // Tabla
                // =========================
                entity.ToTable("paciente");

                // =========================
                // Primary Key
                // =========================
                entity.HasKey(e => e.IdPaciente)
                      .HasName("pk_paciente");

                entity.Property(e => e.IdPaciente)
                      .HasColumnName("id")
                      .HasColumnType("uuid")
                      .HasDefaultValueSql("uuid_generate_v4()")
                      .ValueGeneratedOnAdd();

                // =========================
                // Código de paciente
                // =========================
                entity.Property(e => e.CodigoPaciente)
                      .HasColumnName("codigo")
                      .HasMaxLength(50)
                      .IsRequired();

                // =========================
                // Identificación
                // =========================
                entity.Property(e => e.Identificacion)
                      .HasColumnName("identificacion")
                      .HasMaxLength(15)
                      .IsRequired();

                // =========================
                // Nombres
                // =========================
                entity.Property(e => e.PrimerNombre)
                      .HasColumnName("primernombre")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.SegundoNombre)
                      .HasColumnName("segundonombre")
                      .HasMaxLength(150);

                // =========================
                // Apellidos
                // =========================
                entity.Property(e => e.PrimerApellido)
                      .HasColumnName("primerapellido")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.SegundoApellido)
                      .HasColumnName("segundoapellido")
                      .HasMaxLength(150);

                // =========================
                // genero
                // =========================
                entity.Property(e => e.Genero)
                      .HasColumnName("genero")
                      .HasMaxLength(56);
                // =========================
                // Fecha de nacimiento
                // =========================
                entity.Property(e => e.FechaNacimiento)
                      .HasColumnName("fechanacimiento")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();
                // =========================
                // email
                // =========================
                entity.Property(e => e.Email)
                      .HasColumnName("email") //  respetando el typo en BD
                      .HasMaxLength(156)
                      .IsRequired();

                // =========================
                // Teléfono
                // =========================
                entity.Property(e => e.Telefono)
                      .HasColumnName("telefono")
                      .HasMaxLength(15);

            

                // =========================
                // Activo
                // =========================
                entity.Property(e => e.Activo)
                      .HasColumnName("activo")
                      .HasDefaultValue(true)
                      .IsRequired();

                // =========================
                // Fecha de creación
                // =========================
                entity.Property(e => e.FechaCreacion)
                      .HasColumnName("fechacreacion")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();

           
            });

            // entidad ordenes clinicas
            modelBuilder.Entity<OrdenClinica>(entity =>
            {
                // =========================
                // Tabla
                // =========================
                entity.ToTable("ordenclinica");

                // =========================
                // Primary Key
                // =========================
                entity.HasKey(e => e.IdOrdenClinica)
                      .HasName("pk_orden");

                entity.Property(e => e.IdOrdenClinica)
                      .HasColumnName("id")
                      .HasColumnType("uuid")
                      .HasDefaultValueSql("uuid_generate_v4()")
                      .ValueGeneratedOnAdd();

                // =========================
                // Código de IdEmpresa
                // =========================
                entity.Property(e => e.IdEmpresa)
                      .HasColumnName("idempresa")
                      .HasColumnType("uuid");
                // =========================
                // Código de idpaciente
                // =========================
                entity.Property(e => e.IdPaciente)
                      .HasColumnName("idpaciente")
                      .HasColumnType("uuid");

                // =========================
                // numeroorden
                // =========================
                entity.Property(e => e.NumeroOrden)
                      .HasColumnName("numeroorden")
                      .HasMaxLength(150)
                      .IsRequired();

                
                // =========================
                // Fecha de orden
                // =========================
                entity.Property(e => e.FechaOrden)
                      .HasColumnName("fechaorden")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();
                // =========================
                // estado
                // =========================
                entity.Property(e => e.Estado)
                      .HasColumnName("estado") //  respetando el typo en BD
                      .HasMaxLength(156)
                      .IsRequired();

                // =========================
                // prioridad
                // =========================
                entity.Property(e => e.Prioridad)
                      .HasColumnName("prioridad")
                      .HasMaxLength(15);


                // =========================
                // Fecha de creación
                // =========================
                entity.Property(e => e.FechaCreacion)
                      .HasColumnName("fechacreacion")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();


            });

            // paramtero laboratorio
            modelBuilder.Entity<ParametroLaboratorio>(entity =>
            {
                // =========================
                // Tabla
                // =========================
                entity.ToTable("parametrolaboratorio");

                // =========================
                // Primary Key
                // =========================
                entity.HasKey(e => e.IdParametroLaboratorio)
                      .HasName("pk_parametrolaboratorio");

                entity.Property(e => e.IdParametroLaboratorio)
                      .HasColumnName("id")
                      .HasColumnType("uuid")
                      .HasDefaultValueSql("uuid_generate_v4()")
                      .ValueGeneratedOnAdd();

                // =========================
                // Código de idempresa
                // =========================
                entity.Property(e => e.IdEmpresa)
                      .HasColumnName("idempresa")
                      .HasColumnType("uuid");
                // =========================
                // Código
                // =========================
                entity.Property(e => e.Codigo)
                    .HasColumnName("numeroorden")
                      .HasMaxLength(150)
                      .IsRequired();

                // =========================
                // numeroorden
                // =========================
                entity.Property(e => e.Nombre)
                      .HasColumnName("nombre")
                      .HasMaxLength(150)
                      .IsRequired();
                // =========================
                // UnidadMedida
                // =========================
                entity.Property(e => e.UnidadMedida)
                      .HasColumnName("unidadmedida")
                      .HasMaxLength(150)
                      .IsRequired();

                // =========================
                // tipo dato
                // =========================
                entity.Property(e => e.TipoDato)
                      .HasColumnName("tipodato")
                      .HasMaxLength(150)
                      .IsRequired();
                // =========================
                // tipo dato
                // =========================
                entity.Property(e => e.ValorMinimo)
                      .HasColumnName("valorminimo");
                // =========================
                // valormaximo
                // =========================
                entity.Property(e => e.ValorMinimo)
                      .HasColumnName("valormaximo");


                // =========================
                // Activo
                // =========================
                entity.Property(e => e.Activo)
                      .HasColumnName("activo")
                      .HasDefaultValue(true)
                      .IsRequired();


                // =========================
                // Fecha de creación
                // =========================
                entity.Property(e => e.FechaCreacion)
                      .HasColumnName("fechacreacion")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();


            });

            // resultado laboratorio
            modelBuilder.Entity<ResultadoLaboratorio>(entity =>
            {
                // =========================
                // Tabla
                // =========================
                entity.ToTable("resultadolaboratorio");

                // =========================
                // Primary Key
                // =========================
                entity.HasKey(e => e.IdResultadoLaboratorio)
                      .HasName("pk_usuario");

                entity.Property(e => e.IdResultadoLaboratorio)
                      .HasColumnName("id")
                      .HasColumnType("uuid")
                      .HasDefaultValueSql("uuid_generate_v4()")
                      .ValueGeneratedOnAdd();

                // =========================
                // idempresa
                // =========================
                entity.Property(e => e.IdEmpresa)
                      .HasColumnName("idempresa")
                      .IsRequired();
                // =========================
                // idordenclinica
                // =========================
                entity.Property(e => e.IdOrdenClinica)
                      .HasColumnName("idordenclinica")
                      .IsRequired();
                // =========================
                // IdParametroLaboratorio
                // =========================
                entity.Property(e => e.IdParametroLaboratorio)
                      .HasColumnName("idparametrolaboratorio")
                      .IsRequired();

                // =========================
                // Identificación
                // =========================
                entity.Property(e => e.Examen)
                      .HasColumnName("examen")
                      .HasMaxLength(150)
                      .IsRequired();

                // =========================
                // Nombres
                // =========================
                entity.Property(e => e.Resultado)
                      .HasColumnName("resulatdo")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.ResultadoMedico)
                      .HasColumnName("resultadomedico")
                      .HasMaxLength(150);

                // =========================
                // Apellidos
                // =========================
                entity.Property(e => e.ObservacionTecnica)
                      .HasColumnName("observaciontecnica")
                      .HasMaxLength(150)
                      .IsRequired();

                entity.Property(e => e.UsuarioValida)
                      .HasColumnName("usuariovalida")
                      .HasMaxLength(150);

                // =========================
                // Email
                // =========================
                entity.Property(e => e.ValorNumerico)
                      .HasColumnName("valornumerico");
                // =========================
                // Email
                // =========================
                entity.Property(e => e.Flapatologico)
                      .HasColumnName("flapatologico")
                       .HasDefaultValue(true);

                // =========================
                // Teléfono
                // =========================
                entity.Property(e => e.UnidadMedida)
                      .HasColumnName("unidadmedida")
                      .HasMaxLength(150);

                // =========================
                // Tipo de usuario
                // =========================
                entity.Property(e => e.ValorMinimo)
                      .IsRequired();

                entity.Property(e => e.ValorMaximo)
                     .IsRequired();

                entity.Property(e => e.EstadoResultado)
                     .HasColumnName("estadoresultado")
                     .HasMaxLength(150);
              
                // =========================
                // Fecha de creación
                // =========================
                entity.Property(e => e.FechaCreacion)
                      .HasColumnName("fechacreacion")
                      .HasColumnType("timestamp without time zone")
                      .HasDefaultValueSql("now()")
                      .IsRequired();

               
            });

        }
    }
}
