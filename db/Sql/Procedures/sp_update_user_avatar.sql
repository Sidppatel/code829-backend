CREATE OR REPLACE FUNCTION sp_update_user_avatar(p_user_id uuid, p_avatar_path text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE users SET "AvatarPath" = p_avatar_path, "UpdatedAt" = now() WHERE "Id" = p_user_id;
END; $$;