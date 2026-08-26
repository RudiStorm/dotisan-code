using Dotisan.TypeScript;

namespace Dotisan.AspNetCore.Tests;

public sealed class TypeScriptTypeMapperTests
{
    [Fact]
    public void Nullable_and_optional_contract_semantics_are_distinct()
    {
        Assert.Equal("string | null", TypeScriptTypeMapper.Map(ContractType.String, nullable: true));
        Assert.Equal("number | undefined", TypeScriptTypeMapper.Map(ContractType.Decimal, optional: true));
    }
}
