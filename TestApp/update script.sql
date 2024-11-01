DO $$
DECLARE
    room_id INT;
    new_temp NUMERIC;
    i INT;
BEGIN
    FOR i IN 1..10 LOOP  -- Adjust the number of updates as needed
        -- Select a random row ID from RoomTemperatures to update
        SELECT "Id" INTO room_id 
        FROM public."RoomTemperatures" 
        ORDER BY random() 
        LIMIT 1;
        
        -- Generate a random temperature
        new_temp := round((random() * 15 + 15)::NUMERIC, 1);  -- Generates a random temperature between 15-30
        
        -- Update the selected row with the new temperature and update the timestamp
        UPDATE public."RoomTemperatures"
        SET "CurrentTemperature" = new_temp,
            "UpdatedAt" = NOW()
        WHERE "Id" = room_id;

        -- Pause for 1 second before the next update
        PERFORM pg_sleep(1);
    END LOOP;
END $$;
