using System.ComponentModel.DataAnnotations;
using Anvil.Persistence.Postgres.Extensions;
using Anvil.Primitives;
using Anvil.Primitives.Extensions;
using Anvil.Primitives.Validations;
using Anvil.Security;
using Anvil.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Anvil.ArchitectureTests;

public sealed class CoreAndInfrastructureUnitTests
{
    private sealed class SampleCustomerDto
    {
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; init; } = string.Empty;

        [Cpf(ErrorMessage = "Invalid CPF.")]
        public string Cpf { get; init; } = string.Empty;

        [Cep(ErrorMessage = "Invalid CEP.")]
        public string Cep { get; init; } = string.Empty;
    }

    private sealed class SampleCustomerEntity : Entity<Guid>
    {
        public string Document { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    private sealed class SampleTestDbContext : DbContext
    {
        public SampleTestDbContext(DbContextOptions<SampleTestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SampleCustomerEntity>(builder =>
            {
                builder.ToTable("customers");
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).HasColumnName("id");
                builder.Property(x => x.Created).HasColumnName("created");
                builder.Property(x => x.Updated).HasColumnName("updated");
                builder.Property(x => x.Document).HasColumnName("document");
                builder.Property(x => x.FullName).HasColumnName("full_name");
            });

            modelBuilder.ApplyDatabaseManagedConventions();
        }
    }

    [Fact]
    public void Result_JsonSerialization_Should_Omit_IsFailure_And_RoundTrip_Violations()
    {
        var original = Result.Failure<string>(Error.Validation(nameof(SampleCustomerDto.Cep), "The provided CEP is invalid."));
        var json = original.JsonSerialize();
        Assert.NotNull(json);
        Assert.DoesNotContain("IsFailure", json, StringComparison.OrdinalIgnoreCase);

        var deserialized = json.JsonDeserialize<Result<string>>();
        Assert.False(deserialized.IsSuccess);
        Assert.True(deserialized.IsFailure);
        Assert.Equal(ErrorCode.InvalidArgument.Id, deserialized.Error.Code.Id);
        Assert.Equal(ErrorCode.InvalidArgument.Name, deserialized.Error.Code.Name);
        Assert.NotNull(deserialized.Error.Violations);
        Assert.Single(deserialized.Error.Violations);
        Assert.Equal(nameof(SampleCustomerDto.Cep), deserialized.Error.Violations[0].Field);
    }

    [Fact]
    public void DataAnnotationValidator_Should_Capture_FieldViolations_From_Dto_Attributes()
    {
        var invalidDto = new SampleCustomerDto
        {
            Name = string.Empty,
            Cpf = "11111111111",
            Cep = "123"
        };

        var validationResult = DataAnnotationValidator.Validate(invalidDto);
        Assert.True(validationResult.IsFailure);
        Assert.Equal(ErrorCode.InvalidArgument, validationResult.Error.Code);
        Assert.NotNull(validationResult.Error.Violations);
        Assert.Equal(3, validationResult.Error.Violations.Count);
        Assert.Contains(validationResult.Error.Violations, v => v.Field == nameof(SampleCustomerDto.Name));
        Assert.Contains(validationResult.Error.Violations, v => v.Field == nameof(SampleCustomerDto.Cpf));
        Assert.Contains(validationResult.Error.Violations, v => v.Field == nameof(SampleCustomerDto.Cep));
    }

    [Fact]
    public void CpfValidator_And_TestDataGenerator_Should_Validate_Correctly()
    {
        var generatedCpf = TestDataGenerator.GenerateCpf();
        Assert.True(generatedCpf.IsValidCpf());
        Assert.False("00000000000".IsValidCpf());
        Assert.True("41900000".IsValidCep());
        Assert.False("4190000".IsValidCep());
    }

    [Fact]
    public void CryptoUtils_Should_Encrypt_And_Decrypt_With_Aes256Gcm()
    {
        const string secret = "Sensitive-Payload-2026";
        const string passKey = "Strong-Master-Key-998877";

        var cipherText = CryptoUtils.EncryptToBase64(secret, passKey);
        var plainText = CryptoUtils.DecryptFromBase64(cipherText, passKey);

        Assert.NotEqual(secret, cipherText);
        Assert.Equal(secret, plainText);
    }

    [Fact]
    public void UpsertCommandBuilder_Should_Exclude_DatabaseManaged_Columns_From_Update()
    {
        var options = new DbContextOptionsBuilder<SampleTestDbContext>().UseNpgsql("Host=localhost;Database=test_db;Username=postgres;Password=postgres").Options;

        using var context = new SampleTestDbContext(options);
        var commandBuilder = new PgCommandBuilder<SampleTestDbContext, SampleCustomerEntity>();

        var entity = new SampleCustomerEntity
        {
            Document = TestDataGenerator.GenerateCpf(),
            FullName = "Italo Architect"
        };

        var (sql, parameters) = commandBuilder.BuildUpsertCommand(context, entity, [x => x.Document]);
        Assert.Contains("INSERT INTO \"customers\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"document\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"full_name\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"id\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"created\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"updated\"", sql, StringComparison.Ordinal);
        Assert.Equal(2, parameters.Count);
    }
}
