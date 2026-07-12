namespace IdleCore
{
    /// <summary>
    /// 속성 시스템 — 불 > 어둠 > 번개 > 불 상성 삼각형.
    /// 몬스터 속성은 챕터별 로테이션, 유닛 속성은 config 정의.
    /// </summary>
    public static class Elements
    {
        public const string Fire = "fire";
        public const string Lightning = "lightning";
        public const string Dark = "dark";

        public static readonly string[] All = { Fire, Lightning, Dark };

        /// <summary>a가 b를 상성으로 이기는가 (불>어둠, 어둠>번개, 번개>불)</summary>
        public static bool Beats(string a, string b) =>
            (a == Fire && b == Dark) || (a == Dark && b == Lightning) || (a == Lightning && b == Fire);

        /// <summary>챕터별 몬스터 속성 로테이션</summary>
        public static string MobElement(int chapter) => All[((chapter - 1) % 3 + 3) % 3];

        public static string Label(string element) => element switch
        {
            Fire => "불",
            Lightning => "번개",
            Dark => "어둠",
            _ => "무",
        };

        /// <summary>짧은 표기 (Unity 기본 폰트는 이모지 미지원 — 한 글자 한글 사용, UI에서 색 입힘)</summary>
        public static string Icon(string element) => element switch
        {
            Fire => "불",
            Lightning => "뇌",
            Dark => "암",
            _ => "무",
        };
    }
}
