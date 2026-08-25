-- The rating goes from ten choices to five stars, and what is already stored comes with it.
--
-- Halved and rounded up, which is the arithmetic the owner asked for on 2026-08-25: a 1 or a 2
-- becomes one star, a 9 or a 10 becomes five, and nothing that was rated ends up unrated. Rounding
-- up rather than down is the difference between a 1 surviving as a star and a 1 disappearing —
-- integer division alone would send it to zero, which is not a rating this application can hold.
--
-- Absent ratings are left absent: the WHERE is what keeps "never rated" from becoming "rated zero".
UPDATE personal_state
SET rating = (rating + 1) / 2
WHERE rating IS NOT NULL;
