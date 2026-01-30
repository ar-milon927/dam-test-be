using System.Collections.Generic;

namespace NinjaDAM.DTO.Asset
{
    public class AdvancedSearchRequestDto
    {
        public string Logic { get; set; } = "AND";
        public List<AdvancedSearchConditionDto> Conditions { get; set; } = new();
        public string? SortBy { get; set; }
        public string? SortDir { get; set; }
        public int Page { get; set; } = 1;
        public int? PageSize { get; set; }
        public Guid? FolderId { get; set; }
    }

    public class AdvancedSearchConditionDto
    {
        public string Field { get; set; } = "FileName";
        public string Operator { get; set; } = "Equals";
        public string? Value { get; set; }
        public string? SecondaryValue { get; set; }
        public List<string>? Values { get; set; }
        public AdvancedRangeDto? Range { get; set; }
        public string? MetadataField { get; set; }
        public string? MetadataLabel { get; set; }
        public string? Unit { get; set; }
    }

    public class AdvancedRangeDto
    {
        public string? From { get; set; }
        public string? To { get; set; }
    }
}

