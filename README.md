# LSM 최소 실행 초안 (싱글스레드 작업 큐)

이 저장소는 **단일 워커(싱글스레드)** 방식으로 작업 큐를 순차 실행하는 최소 버전입니다.

## 구성
- `queue.json` : 작업 큐(PENDING/RUNNING/DONE/BLOCKED)
- `app/worker.py` : 큐 1건씩 실행하는 워커
- `logs/` : 실행 로그

## 요구사항
- Python 3.10+
- Git CLI (`git`)

## 빠른 시작
```bash
cd /mnt/c/project/lsm
python3 app/worker.py run-once --repo .
```

## 큐 포맷 (`queue.json`)
```json
{
  "tasks": [
    {
      "id": "task-001",
      "title": "샘플 파일 생성",
      "status": "PENDING",
      "command": "echo 'hello lsm' > hello.txt",
      "commit_message": "초안: 샘플 파일 생성"
    }
  ]
}
```

## 실행 모드
- 1회 실행:
  ```bash
  python3 app/worker.py run-once --repo .
  ```
- 루프 실행(지정초 간격):
  ```bash
  python3 app/worker.py run-loop --repo . --interval 30
  ```

## 안전 옵션
기본 동작은 **로컬 커밋까지만** 수행합니다.
- 원격 푸시까지: `--push`
- 자동 머지 플래그는 본 초안에서 미구현(향후 확장)

## 현재 범위
- 싱글스레드 직렬 실행
- 실패 시 BLOCKED 처리 + 에러 로그 기록
- 작업당 브랜치 생성(`task/<id>`)
- 변경사항이 있으면 자동 커밋

향후 확장:
- GitHub PR 생성/머지(gh CLI)
- 재시도 횟수/백오프 정책
- CI 통과 확인 후 자동머지
