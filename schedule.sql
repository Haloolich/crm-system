CREATE TABLE clients (
    id INT AUTO_INCREMENT PRIMARY KEY,
    phone VARCHAR(15) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL
);

-- Таблиця бронювань
CREATE TABLE bookings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    client_id INT NOT NULL,
    day DATE NOT NULL,
    zone VARCHAR(20) NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    num_people INT NOT NULL,
    FOREIGN KEY (client_id) REFERENCES clients(id)
);