
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    surname VARCHAR(100) NOT NULL,
    name VARCHAR(100) NOT NULL,
    patronymic VARCHAR(100),
    email VARCHAR(200) UNIQUE NOT NULL,
    phone_number VARCHAR(20) NOT NULL,
    driver_license_number VARCHAR(20) UNIQUE NOT NULL,
    date_of_birth DATE NOT NULL,
    registration_date TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE cars (
    id SERIAL PRIMARY KEY,
    brand VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    year INT NOT NULL,
    license_plate VARCHAR(20) UNIQUE NOT NULL,
    price_per_day DECIMAL(10,2) NOT NULL CHECK (price_per_day > 0),
    is_available BOOLEAN NOT NULL DEFAULT TRUE,
    color VARCHAR(50)
);

CREATE TABLE locations (
    id SERIAL PRIMARY KEY,
    city VARCHAR(100) NOT NULL,
    address VARCHAR(200) NOT NULL,
    phone VARCHAR(20),
    working_hours VARCHAR(50)
);

CREATE TABLE rentals (
    id SERIAL PRIMARY KEY,
    customer_id INT NOT NULL REFERENCES customers(id),
    car_id INT NOT NULL REFERENCES cars(id),
    pickup_location_id INT NOT NULL REFERENCES locations(id),
    return_location_id INT REFERENCES locations(id),
    start_date TIMESTAMP NOT NULL,
    end_date TIMESTAMP NOT NULL,
    actual_return_date TIMESTAMP,
    total_cost DECIMAL(10,2) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Active'
);

CREATE TABLE payments (
    id SERIAL PRIMARY KEY,
    rental_id INT NOT NULL REFERENCES rentals(id),
    total_summer DECIMAL(10,2) NOT NULL,
    payment_date TIMESTAMP NOT NULL DEFAULT NOW(),
    payment_method VARCHAR(50),
    status VARCHAR(20) NOT NULL DEFAULT 'Paid'
);


INSERT INTO customers (surname, name, patronymic, email, phone_number, driver_license_number, date_of_birth)
VALUES
('Стальмаченок', 'Полина', 'Михайловна', 'poli@gmail.ru', '+375291234567', 'AB1234567', '2003-05-15'),
('Шкиндер', 'Вероника', 'Валерьевна', 'kind@gmail.ru', '+375297654321', 'CD7654321', '1999-11-03'),
('Астукевич', 'Даниил', 'Алексеевич', 'dany@mail.ru', '+375295556677', 'EF9876543', '2006-04-26');

INSERT INTO cars (brand, model, year, license_plate, price_per_day, is_available, color)
VALUES
('Toyota', 'Camry', 2020, 'А123ВС', 45.00, TRUE, 'Серебристый'),
('BMW', 'X5', 2021, 'В456ХК', 75.00, TRUE, 'Черный'),
('Kia', 'Rio', 2022, 'С789ОР', 30.00, TRUE, 'Белый'),
('Hyundai', 'Solaris', 2021, 'Е321КМ', 35.00, TRUE, 'Красный'),
('Mercedes', 'E-Class', 2022, 'К654НЕ', 90.00, TRUE, 'Синий');

INSERT INTO locations (city, address, phone, working_hours)
VALUES
('Минск', 'ул. Независимости, 15', '+375291111111', '09:00-21:00'),
('Минск', 'ул. Притыцкого, 45', '+375292222222', '08:00-20:00'),
('Гомель', 'ул. Советская, 10', '+375293333333', '09:00-19:00');



select * from rentals



ALTER TABLE payments
DROP CONSTRAINT payments_rental_id_fkey;

ALTER TABLE payments
ADD CONSTRAINT payments_rental_id_fkey
FOREIGN KEY (rental_id) REFERENCES rentals(id) ON DELETE CASCADE;

