DO $$
DECLARE
    room_names TEXT[] := ARRAY['Living Room', 'Bedroom', 'Kitchen', 'Bathroom', 'Office'];
    room_name TEXT;
    temp NUMERIC;
    i INT;
BEGIN
    FOR i IN 1..10 LOOP  -- Adjust the number of inserts as needed
        -- Ensure the array index is always within bounds by truncating to the array length
        room_name := room_names[(floor(random() * array_length(room_names, 1)) + 1)::INT];
        temp := round((random() * 15 + 15)::NUMERIC, 1); 
        INSERT INTO public."RoomTemperatures" ("RoomName", "CurrentTemperature", "CurrentTime", "CreatedAt", "UpdatedAt")
        VALUES (room_name, temp, NOW(), NOW(), NOW());
		PERFORM pg_sleep(0.5);
    END LOOP;
END $$;
