-- Users (Store user information)
CREATE TABLE users (
    user_id UUID PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);


-- Events
CREATE TABLE events (
    event_id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    price_paise BIGINT NOT NULL,
    per_user_seat_limit INTEGER NOT NULL DEFAULT 4,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT ck_events_price_paise
        CHECK (price_paise >= 0),
    
    CONSTRAINT ck_events_per_user_seat_limit
        CHECK (per_user_seat_limit > 0)
);


-- Seats
CREATE TABLE seats (
    seat_id UUID PRIMARY KEY,
    event_id UUID NOT NULL,
    seat_number VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'available',

    CONSTRAINT fk_seats_event
        FOREIGN KEY (event_id)
        REFERENCES events(event_id),

    CONSTRAINT uq_seats_event_number
        UNIQUE (event_id, seat_number),

    CONSTRAINT ck_seats_status
        CHECK (status IN ('available', 'held', 'confirmed'))
);


-- Reservations
CREATE TABLE reservations (
    reservation_id UUID PRIMARY KEY,
    event_id UUID NOT NULL,
    user_id UUID NOT NULL,
    amount_paise BIGINT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'confirmed',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    cancelled_at TIMESTAMPTZ,

    CONSTRAINT fk_reservations_event
        FOREIGN KEY (event_id)
        REFERENCES events(event_id),

    CONSTRAINT fk_reservations_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id),

    CONSTRAINT ck_reservations_amount
        CHECK (amount_paise >= 0),

    CONSTRAINT ck_reservations_status
        CHECK (status IN ('confirmed', 'cancelled'))
);


-- Seats belonging to a reservation (To track which seats are reserved under a reservation)
CREATE TABLE reservation_seats (
    reservation_id UUID NOT NULL,
    seat_id UUID NOT NULL,

    PRIMARY KEY (reservation_id, seat_id),

    CONSTRAINT fk_reservation_seats_reservation
        FOREIGN KEY (reservation_id)
        REFERENCES reservations(reservation_id),

    CONSTRAINT fk_reservation_seats_seat
        FOREIGN KEY (seat_id)
        REFERENCES seats(seat_id)
);


-- Idempotency (To ensure that the same request doesn't create multiple reservations)
CREATE TABLE idempotency_keys (
    idempotency_key VARCHAR(255) NOT NULL,
    event_id UUID NOT NULL,
    user_id UUID NOT NULL,
    request_hash VARCHAR(64) NOT NULL,
    reservation_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    PRIMARY KEY (event_id, user_id, idempotency_key),

    CONSTRAINT fk_idempotency_event
        FOREIGN KEY (event_id)
        REFERENCES events(event_id),

    CONSTRAINT fk_idempotency_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id),

    CONSTRAINT fk_idempotency_reservation
        FOREIGN KEY (reservation_id)
        REFERENCES reservations(reservation_id)
);

-- Indexes
CREATE INDEX idx_seats_event_status
    ON seats(event_id, status);

CREATE INDEX idx_reservations_event_user
    ON reservations(event_id, user_id);

CREATE INDEX idx_reservation_seats_seat
    ON reservation_seats(seat_id);