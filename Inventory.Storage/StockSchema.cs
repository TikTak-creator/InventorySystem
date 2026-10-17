namespace Inventory.Storage;

internal static class StockSchema
{
    public static readonly string[] Schema =
    {
        @"CREATE TABLE IF NOT EXISTS products (
            id             INT AUTO_INCREMENT PRIMARY KEY,
            sku            VARCHAR(50)   NOT NULL UNIQUE,
            name           VARCHAR(200)  NOT NULL,
            category       VARCHAR(100)  NULL,
            purchase_price DECIMAL(12,2) NOT NULL DEFAULT 0,
            price          DECIMAL(12,2) NOT NULL DEFAULT 0,
            quantity       INT           NOT NULL DEFAULT 0,
            min_quantity   INT           NOT NULL DEFAULT 0,
            created_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            INDEX ix_products_name (name),
            INDEX ix_products_category (category)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS sales (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            created_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            total      DECIMAL(12,2) NOT NULL,
            INDEX ix_sales_created (created_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS sale_items (
            id           INT AUTO_INCREMENT PRIMARY KEY,
            sale_id      INT           NOT NULL,
            product_id   INT           NULL,
            product_name VARCHAR(200)  NOT NULL,
            quantity     INT           NOT NULL,
            price        DECIMAL(12,2) NOT NULL,
            cost_price   DECIMAL(12,2) NOT NULL DEFAULT 0,
            CONSTRAINT fk_items_sale    FOREIGN KEY (sale_id)    REFERENCES sales(id)    ON DELETE CASCADE,
            CONSTRAINT fk_items_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE SET NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",

        @"CREATE TABLE IF NOT EXISTS price_history (
            id         INT AUTO_INCREMENT PRIMARY KEY,
            product_id INT           NOT NULL,
            old_price  DECIMAL(12,2) NOT NULL,
            new_price  DECIMAL(12,2) NOT NULL,
            changed_at DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_price_product FOREIGN KEY (product_id) REFERENCES products(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;",
        """
        CREATE TABLE IF NOT EXISTS sale_requests (
            user_id INT NOT NULL,
            request_key CHAR(36) NOT NULL,
            request_hash CHAR(64) NOT NULL,
            sale_id INT NULL,
            PRIMARY KEY (user_id, request_key),
            FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """
    };

}
