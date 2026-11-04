// OpenAPI 문서의 스키마를 실제 응답 직렬화와 맞춘다 (열거형 컴포넌트의 null 빼기, 실제로 null 이 오는 객체 칸 nullable 로)
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ashveil.Server.Infrastructure;

public static class OpenApiSchemaFixes
{
    // shared DTO 는 Unity(C# 9) 와 같이 쓰느라 nullable 주석이 없어서, 문서 생성기는 객체 칸에 null 이 오는지 모른다.
    // 응답에서 실제로 null 을 돌려주는 객체 칸만 적는다(문자열 · 배열 칸은 생성기가 이미 null 을 허용한다).
    private static readonly (string Schema, string Property)[] NullableObjects =
    [
        ("EnhanceResponse", "item"),        // 파괴되면 null
        ("BotKillResponse", "dropped"),     // 전리품이 없으면 null
        ("PlayerDeathResponse", "dropped"), // 떨어뜨린 아이템이 없으면 null
        ("PickupResponse", "inventory"),    // 봇이 주우면 null
    ];

    public static Task Apply(OpenApiDocument doc, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemas = doc.Components!.Schemas!;
        // EquipSlot? 처럼 nullable 칸에서 처음 만난 열거형은 컴포넌트 enum 에 null 이 들어간다. 칸 쪽은 이미 oneOf [null, $ref] 라
        // null 값이 두 갈래에 다 맞아 oneOf(정확히 한 갈래) 검사가 실패한다.
        foreach (var schema in schemas.Values.OfType<OpenApiSchema>().Where(s => s.Enum != null))
            schema.Enum = schema.Enum!.Where(v => v != null && v.GetValueKind() != JsonValueKind.Null).ToList();

        foreach (var (name, property) in NullableObjects)
        {
            var properties = ((OpenApiSchema)schemas[name]).Properties!;
            properties[property] = new OpenApiSchema { OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, properties[property]] };
        }
        return Task.CompletedTask;
    }
}
