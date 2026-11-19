using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;   // JArray( [ ] = List 같은 것 ), JObject( { } = Dictionary 같은 것 )
using UnityEngine;

namespace Game.Table
{
    // "적 데이터의 로드 / 키 해석 / 보관 / 조회"의 책임을 갖는 객체
    // 교안 EnemyTable 과 같은 구조. 다른 곳은 세 군데다.
    //   ① 파일을 어디서 읽는가   : StreamingAssets → Resources
    //   ② 한 행을 어떻게 자르는가 : Split('\t') 후 번호로 꺼내기 → JSON 덩어리에서 이름으로 꺼내기
    //   ③ 행 안에 또 행이 있다   : 적 한 명 안에 라운드 5줄 → foreach 안에 foreach
    public class EnemyTable
    {
        // Resources 폴더 기준 경로. 확장자(.json)는 붙이지 않는다.
        // → Assets/Resources/Table/enemies.json
        private const string TableName = "Table/enemies";

        // 키 = 난이도. 난이도 하나에 적 한 명.
        private Dictionary<Difficulty, EnemyDef> defs = new();

        public EnemyDef Get(Difficulty difficulty) => defs[difficulty];

        public IReadOnlyCollection<EnemyDef> GetDefs() => defs.Values;

        public int Count => defs.Count;

        public void Load()
        {
            defs.Clear();

            // ① 파일을 통째로 글자로 읽는다
            //    Quest(Android) 빌드에서는 StreamingAssets 가 APK 안에 들어가서 StreamReader 로 못 연다.
            //    그래서 Resources 에 두고 TextAsset 으로 읽는다. (.json 파일은 유니티가 TextAsset 으로 가져온다)
            TextAsset file = Resources.Load<TextAsset>(TableName);
            string json = file.text;

            // ② 글자를 JSON 덩어리로 바꾼다
            //    파일이 [ ... ] 로 시작하므로 JArray(목록)로 읽는다.
            JArray array = JArray.Parse(json);

            List<EnemyDTO> rows = new List<EnemyDTO>();
            foreach (JObject enemyRow in array)   // 바깥 [ ] 안의 { } 하나 = 적 한 명
            {
                // enemyRow : { "Difficulty": "Easy", "PrefabKey": "Enemies/SM_Chr_Cowboy_01", "BaseScore": 700, "Rounds": [ ... ] }
                EnemyDTO enemyDto = new EnemyDTO();

                // 글자 "Easy" → Difficulty.Easy  (int.Parse 가 글자를 숫자로 바꾸듯, Enum.Parse 는 글자를 enum 으로 바꾼다)
                enemyDto.Difficulty = Enum.Parse<Difficulty>((string)enemyRow["Difficulty"]);
                enemyDto.PrefabKey = (string)enemyRow["PrefabKey"];
                enemyDto.BaseScore = (int)enemyRow["BaseScore"];

                // "Rounds": [ {...}, {...} ]  ← 적 한 명 안에 라운드 목록이 또 있다. 한 번 더 foreach
                JArray roundArray = (JArray)enemyRow["Rounds"];
                foreach (JObject roundRow in roundArray)
                {
                    // roundRow : { "Round": 1, "Distance": 10, "FireTime": 1.30, "HitRate": 0.20 }
                    RoundDef round = new RoundDef();
                    round.Round = (int)roundRow["Round"];
                    round.Distance = (float)roundRow["Distance"];
                    round.FireTime = (float)roundRow["FireTime"];
                    round.HitRate = (float)roundRow["HitRate"];

                    // GetRound 가 "칸 번호 = 라운드 번호 - 1" 로 꺼내므로, 파일에 1라운드부터 순서대로 있어야 한다
                    if (round.Round != enemyDto.Rounds.Count + 1)
                        throw new Exception($"[EnemyTable] {enemyDto.Difficulty} : 라운드 순서가 어긋남 ({enemyDto.Rounds.Count + 1}라운드 자리에 {round.Round}라운드)");

                    enemyDto.Rounds.Add(round);
                }

                rows.Add(enemyDto);
            }

            // ③ List<EnemyDTO> rows 를 이용해서 def 객체를 생성한다 (키 해석)
            foreach (EnemyDTO enemyDto in rows)
            {
                // Resources.Load 는 못 찾아도 에러 없이 null 을 준다.
                // 그대로 두면 한참 뒤 Instantiate 에서야 터지므로, 키가 보이는 지금 바로 멈춘다.
                GameObject prefab = Resources.Load<GameObject>(enemyDto.PrefabKey);
                if (prefab == null)
                    throw new Exception($"[EnemyTable] {enemyDto.Difficulty} : 적 프리팹을 찾지 못함 → Resources/{enemyDto.PrefabKey}");

                defs[enemyDto.Difficulty] = new EnemyDef
                {
                    Difficulty = enemyDto.Difficulty,
                    Prefab = prefab,
                    BaseScore = enemyDto.BaseScore,   // 숫자라서 해석할 것이 없다. 그대로 넘긴다
                    Rounds = enemyDto.Rounds,   // 라운드는 해석할 것이 없어서 그대로 넘긴다
                };
            }

            Debug.Log($"[EnemyTable] 로드 완료 — 적 {defs.Count}명");
        }
    }
}
