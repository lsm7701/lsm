#!/usr/bin/env python3
import argparse
import json
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEUE_PATH = ROOT / "queue.json"
LOG_DIR = ROOT / "logs"


def now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def run(cmd: str, cwd: Path) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, shell=True, cwd=str(cwd), text=True, capture_output=True)


def git(cmd: str, repo: Path) -> subprocess.CompletedProcess:
    return run(f"git {cmd}", repo)


def load_queue() -> dict:
    if not QUEUE_PATH.exists():
        return {"tasks": []}
    return json.loads(QUEUE_PATH.read_text(encoding="utf-8"))


def save_queue(data: dict) -> None:
    QUEUE_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def pick_next_task(data: dict):
    for task in data.get("tasks", []):
        if task.get("status") == "PENDING":
            return task
    return None


def write_log(task_id: str, text: str) -> Path:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    path = LOG_DIR / f"{task_id}.log"
    path.write_text(text, encoding="utf-8")
    return path


def ensure_git_clean(repo: Path) -> None:
    st = git("status --porcelain", repo)
    if st.returncode != 0:
        raise RuntimeError(st.stderr.strip() or "git status 실패")


def process_task(task: dict, repo: Path, push: bool) -> tuple[bool, str]:
    task_id = task["id"]
    branch = f"task/{task_id}"

    # 브랜치 준비
    git("fetch --all --prune", repo)
    co = git(f"checkout -B {branch}", repo)
    if co.returncode != 0:
        return False, f"브랜치 생성 실패: {co.stderr.strip()}"

    # 사용자 명령 실행
    user = run(task["command"], repo)
    log_text = (
        f"[{now()}] TASK={task_id}\n"
        f"CMD: {task['command']}\n\n"
        f"[STDOUT]\n{user.stdout}\n"
        f"[STDERR]\n{user.stderr}\n"
        f"[RETURN_CODE] {user.returncode}\n"
    )
    log_path = write_log(task_id, log_text)

    if user.returncode != 0:
        return False, f"작업 명령 실패 (log: {log_path})"

    # 변경 확인/커밋
    diff = git("status --porcelain", repo)
    if diff.returncode != 0:
        return False, f"변경 확인 실패: {diff.stderr.strip()}"

    if diff.stdout.strip():
        add = git("add -A", repo)
        if add.returncode != 0:
            return False, f"git add 실패: {add.stderr.strip()}"

        msg = task.get("commit_message") or f"작업 반영: {task_id}"
        cm = git(f"commit -m \"{msg}\"", repo)
        if cm.returncode != 0:
            return False, f"커밋 실패: {cm.stderr.strip()}"

        if push:
            ps = git(f"push -u origin {branch}", repo)
            if ps.returncode != 0:
                return False, f"푸시 실패: {ps.stderr.strip()}"
    else:
        # 변경이 없는 경우도 DONE 처리
        pass

    return True, f"완료 (log: {log_path})"


def run_once(repo: Path, push: bool) -> int:
    data = load_queue()
    task = pick_next_task(data)
    if not task:
        print("PENDING 작업 없음")
        return 0

    task["status"] = "RUNNING"
    task["updated_at"] = now()
    save_queue(data)

    ok, msg = process_task(task, repo, push)
    task["updated_at"] = now()
    if ok:
        task["status"] = "DONE"
        task["result"] = msg
        print(f"[DONE] {task['id']} - {msg}")
        save_queue(data)
        return 0

    task["status"] = "BLOCKED"
    task["result"] = msg
    save_queue(data)
    print(f"[BLOCKED] {task['id']} - {msg}")
    return 1


def run_loop(repo: Path, push: bool, interval: int) -> int:
    while True:
        rc = run_once(repo, push)
        if rc != 0:
            return rc
        time.sleep(interval)


def main() -> int:
    parser = argparse.ArgumentParser(description="LSM 싱글스레드 작업 큐 워커")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p1 = sub.add_parser("run-once")
    p1.add_argument("--repo", default=str(ROOT), help="git 저장소 경로")
    p1.add_argument("--push", action="store_true", help="커밋 후 원격 푸시")

    p2 = sub.add_parser("run-loop")
    p2.add_argument("--repo", default=str(ROOT), help="git 저장소 경로")
    p2.add_argument("--push", action="store_true", help="커밋 후 원격 푸시")
    p2.add_argument("--interval", type=int, default=30, help="루프 간격(초)")

    args = parser.parse_args()
    repo = Path(args.repo).resolve()

    if not (repo / ".git").exists():
        print(f"git 저장소가 아님: {repo}")
        return 2

    if args.cmd == "run-once":
        return run_once(repo, args.push)
    if args.cmd == "run-loop":
        return run_loop(repo, args.push, args.interval)

    return 0


if __name__ == "__main__":
    sys.exit(main())
