# LSM Jules 직렬 큐

작성일: 2026-02-26
원칙: 병렬 금지, 1건씩 처리

## 상태 코드
- PENDING / RUNNING / DONE / BLOCKED

## 큐
| 우선순위 | 항목 | 상태 | 완료 조건 |
|---|---|---|---|
| 1 | SkillEditorWindow TreeView 골격 도입 | DONE | 탭별 트리/노드 추가삭제/종합 연결 트리 반영 + 커밋 완료 |
| 2 | JSON Import/Export 구현 (skill/sequence/affect/condition) | PENDING | 4개 파일 읽기/쓰기 버튼 + 라운드트립(불러오기→수정→내보내기) 동작 |
| 3 | 타입별 키 자동 폼 고도화 (eDataInt/Float/String 매핑) | PENDING | 타입 선택 시 허용 키 자동 표시/입력 검증 + 기본 프리셋 확장 |
| 4 | 인덱스 할당 규칙 엔진 ([캐릭터번호][할당인덱스]) | PENDING | 100/200/300/400/500~520/900~940 규칙 지원 + 수동 입력 허용 |
| 5 | 종합 탭 복구/표시 강화 (DESC 기반 복구 포함) | PENDING | 4개 JSON 로드 시 동일 계층 재구성 + 종합 트리 정확 표시 |
| 6 | 검증/오류 패널(중복/끊긴 참조/타입불일치) | PENDING | 오류 목록 표시 + 선택 시 해당 노드 포커스 |

## 메모
- 파일명은 `sequence.json` 기준으로 통일
- txt 참조 파일은 스키마/키 힌트 용도로만 사용 (컴파일 제외)
