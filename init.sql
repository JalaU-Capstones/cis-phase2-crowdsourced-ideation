-- Legacy-compatible schema - DO NOT MODIFY (R3)
-- Database: sd3
-- Table: users

CREATE DATABASE IF NOT EXISTS sd3;
USE sd3;

CREATE TABLE IF NOT EXISTS users (
  id VARCHAR(36) NOT NULL,
  name VARCHAR(200) NOT NULL,
  login VARCHAR(20) NOT NULL,
  password VARCHAR(100) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE INDEX id_UNIQUE (id ASC) VISIBLE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Optional test data (for local development only - remove in production)
-- INSERT INTO users (id, name, login, password)
-- VALUES ('550e8400-e29b-41d4-a716-446655440000', 'Test User', 'testuser', 'test123');

-- -----------------
-- Phase 2: CIS
-- -----------------

CREATE TABLE IF NOT EXISTS topics (
    id VARCHAR(36) NOT NULL,
    PRIMARY KEY (id),
    title VARCHAR(200) NOT NULL,
    description TEXT NULL,
    status ENUM('OPEN', 'CLOSED') NOT NULL DEFAULT 'OPEN',
    owner_id VARCHAR(36) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_topics_users FOREIGN KEY (owner_id) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS ideas(
    id VARCHAR(36) NOT NULL,
    PRIMARY KEY (id),
    content TEXT NOT NULL,
    topic_id VARCHAR(36) NOT NULL,
    owner_id VARCHAR(36) NOT NULL COMMENT 'Only the owner/creator can edit or delete this idea',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_ideas_topics FOREIGN KEY (topic_id) REFERENCES topics(id),
    CONSTRAINT fk_ideas_owner FOREIGN KEY (owner_id) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS votes(
    id VARCHAR(36) NOT NULL,
    PRIMARY KEY (id),
    idea_id VARCHAR(36) NOT NULL,
    user_id VARCHAR(36) NOT NULL,
    CONSTRAINT fk_votes_ideas FOREIGN KEY (idea_id) REFERENCES ideas(id),
    CONSTRAINT fk_votes_users FOREIGN KEY (user_id) REFERENCES users(id),
    UNIQUE KEY uq_votes_idea_user (idea_id, user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;