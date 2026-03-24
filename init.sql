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

