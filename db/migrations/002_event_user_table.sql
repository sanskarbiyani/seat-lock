-- Event Users (Store the relationship between events and users, for row locking)
CREATE TABLE event_users (
    event_id UUID NOT NULL,
    user_id UUID NOT NULL,

    PRIMARY KEY (event_id, user_id),

    CONSTRAINT fk_event_users_event
        FOREIGN KEY (event_id)
        REFERENCES events(event_id),

    CONSTRAINT fk_event_users_user
        FOREIGN KEY (user_id)
        REFERENCES users(user_id)
);