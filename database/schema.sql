-- =====================================================================
--  StockFlow — система учёта товаров
--  Схема базы данных MySQL 8.x
--  StockFlow 2.0: серверная команда init-db или ручное развёртывание.
--  Бизнес-таблицы сохранены; добавлены ключи повторов, пользователи, сессии.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS inventory_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE inventory_db;

-- Товары
CREATE TABLE IF NOT EXISTS products (
    id             INT AUTO_INCREMENT PRIMARY KEY,
    sku            VARCHAR(50)   NOT NULL UNIQUE,          -- артикул
    name           VARCHAR(200)  NOT NULL,
    category       VARCHAR(100)  NULL,
    purchase_price DECIMAL(12,2) NOT NULL DEFAULT 0,       -- закупочная цена
    price          DECIMAL(12,2) NOT NULL DEFAULT 0,       -- цена продажи
    quantity       INT           NOT NULL DEFAULT 0,       -- остаток на складе
    min_quantity   INT           NOT NULL DEFAULT 0,       -- порог «мало на складе»
    created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX ix_products_name (name),
    INDEX ix_products_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Чеки (продажи)
CREATE TABLE IF NOT EXISTS sales (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    created_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total      DECIMAL(12,2) NOT NULL,
    INDEX ix_sales_created (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Позиции чека (название и цены фиксируются на момент продажи)
CREATE TABLE IF NOT EXISTS sale_items (
    id           INT AUTO_INCREMENT PRIMARY KEY,
    sale_id      INT           NOT NULL,
    product_id   INT           NULL,
    product_name VARCHAR(200)  NOT NULL,
    quantity     INT           NOT NULL,
    price        DECIMAL(12,2) NOT NULL,
    cost_price   DECIMAL(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT fk_items_sale    FOREIGN KEY (sale_id)    REFERENCES sales(id)    ON DELETE CASCADE,
    CONSTRAINT fk_items_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- История изменения цен
CREATE TABLE IF NOT EXISTS price_history (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    product_id INT           NOT NULL,
    old_price  DECIMAL(12,2) NOT NULL,
    new_price  DECIMAL(12,2) NOT NULL,
    changed_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_price_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Ключи повторов продаж (создаются и завершаются в транзакции чека).
CREATE TABLE IF NOT EXISTS sale_requests (
    user_id      INT NOT NULL,
    request_key  CHAR(36) NOT NULL,
    request_hash CHAR(64) NOT NULL,
    sale_id      INT NULL,
    PRIMARY KEY (user_id, request_key),
    FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Пользователи приложения; password_hash — формат ASP.NET IdentityV3.
CREATE TABLE IF NOT EXISTS app_users (
    id                  INT AUTO_INCREMENT PRIMARY KEY,
    username            VARCHAR(64) NOT NULL,
    normalized_username VARCHAR(64) NOT NULL UNIQUE,
    password_hash       VARCHAR(512) NOT NULL,
    role                VARCHAR(16) NOT NULL,
    active              BOOLEAN NOT NULL DEFAULT TRUE,
    failed_attempts     INT NOT NULL DEFAULT 0,
    locked_until        DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Только SHA-256-хэш токена, время UTC; исходных токенов здесь нет.
CREATE TABLE IF NOT EXISTS app_sessions (
    token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
    user_id    INT NOT NULL,
    expires_at DATETIME NOT NULL,
    FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE CASCADE,
    INDEX ix_session_expiry (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
