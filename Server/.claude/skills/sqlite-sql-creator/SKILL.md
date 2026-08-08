---
name: sqlite-sql-creator
description: Write SQLite DDL and queries for this project's database (Server/Shared/game.sqlite3). Every table and column must be commented, tables must use STRICT, and indexes are added only when justified with a stated reason. Use when creating or reviewing tables, columns, indexes, or queries for the SQLite database.
---

# SQLite SQL Creator

Produce SQLite SQL for this project (`Server/Shared/game.sqlite3`) that is
correct for SQLite specifically and reads cleanly. Follow every rule below.

---

## Required inputs from the user

Before writing any DDL, you MUST have:

1. **Table name** — the exact table identifier.
2. **What the table holds** — the columns / data that must be stored.

If either is missing or ambiguous, **stop and ask the user**. Do not invent a
schema from assumptions.

---

## Hard rules

1. **Every table uses `STRICT`.** Always append `STRICT` to `CREATE TABLE`.
   This enforces declared column types and avoids SQLite's loose type affinity.
2. **Comments are mandatory.** Every column gets an inline `--` comment, and
   every index gets a comment explaining its purpose. Write comments in Korean
   (project code uses Korean comments).
3. **Indexes are situational, and you must justify them.** Only add an index
   when a real access pattern needs it, and always tell the user *why* you added
   it (or why you did not). See "When to add an index" below.

---

## Type & column conventions

SQLite `STRICT` tables allow only: `INTEGER`, `REAL`, `TEXT`, `BLOB`, `ANY`.

- **Primary key:** prefer `INTEGER PRIMARY KEY` — it is an alias for the `rowid`,
  so it is the fastest lookup key and reuses the built-in index.
- **Booleans:** SQLite has no boolean type. Use `INTEGER NOT NULL DEFAULT 0`
  with a `0/1` convention, and say so in the comment.
- **Timestamps:** store as `TEXT` in UTC with `DEFAULT (datetime('now'))`.
  Keep timestamps UTC and note it in the comment.
- **NOT NULL + DEFAULT:** give columns sensible defaults so inserts stay simple.
- **UNIQUE:** mark natural keys (e.g. an external provider id) `UNIQUE`.

---

## DDL format (follow this exactly)

```sql
CREATE TABLE t_account (
    user_id     INTEGER PRIMARY KEY,                        -- 계정 PK (rowid 별칭)
    provider_id TEXT    NOT NULL UNIQUE,                    -- 외부 제공자 ID
    nickname    TEXT    NOT NULL,                           -- 표시 이름
    admin_level INTEGER NOT NULL DEFAULT 0,                 -- 0=일반, 1+=관리자
    is_deleted  INTEGER NOT NULL DEFAULT 0,                 -- 삭제 여부 (0/1)
    is_banned   INTEGER NOT NULL DEFAULT 0,                 -- 밴 여부 (0/1)
    created_at  TEXT    NOT NULL DEFAULT (datetime('now'))  -- 생성 시각 (UTC)
) STRICT;

-- 인덱스에도 주석을 붙일 수 있음
CREATE INDEX idx_account_created_at ON t_account (created_at);  -- 가입일 정렬/조회용
```

Formatting notes:
- Align column names, types, and constraints into columns so the block scans
  top-to-bottom easily (as above).
- Keep the `-- comment` on the same line as each column.
- Put `STRICT` on the closing line: `) STRICT;`.

---

## When to add an index (and why to say so)

An index speeds up reads on a column but costs write time and disk space, so
add one only for a real access pattern. After proposing DDL, state the index
decision explicitly.

Add an index when a column is:
- **filtered often** in `WHERE` (e.g. lookups by `provider_id`),
- **sorted often** in `ORDER BY` (e.g. listing by `created_at`),
- **joined on** across tables (foreign-key columns).

Do NOT add an index when:
- the column is already covered — `INTEGER PRIMARY KEY` and `UNIQUE` columns are
  indexed automatically, so a separate index is redundant,
- the table is small or write-heavy and reads are rare,
- no query actually filters/sorts/joins on it yet.

Always report the reasoning, e.g.:
> Added `idx_account_created_at` because accounts are listed sorted by join date.
> Did not index `nickname` — no query filters on it yet.

---

## Query guidance

- Write SQL targeting SQLite (not Postgres/MySQL).
- Prefer explicit column lists over `SELECT *`.
- Use CTEs (`WITH`) and window functions for readable multi-step queries.
- Remember SQLite type affinity and `0/1` boolean columns when comparing values.

---

## Checklist before returning SQL

- [ ] Table name and columns were given by the user (asked if not).
- [ ] `STRICT` is on every `CREATE TABLE`.
- [ ] Every column has an inline comment; every index has a comment.
- [ ] Index decisions are stated with reasons (added or intentionally skipped).
- [ ] Types are STRICT-legal (`INTEGER`/`REAL`/`TEXT`/`BLOB`/`ANY`).
