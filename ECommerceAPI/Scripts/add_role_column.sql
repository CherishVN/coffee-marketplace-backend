-- SQL Script để thêm cột role vào bảng users
-- Chạy script này trong Supabase SQL Editor

-- Thêm cột role nếu chưa có
ALTER TABLE public.users 
ADD COLUMN IF NOT EXISTS role TEXT DEFAULT 'customer';

-- Tạo comment cho cột
COMMENT ON COLUMN public.users.role IS 'User role: customer, seller, admin';

-- Cập nhật các user hiện tại không có role
UPDATE public.users 
SET role = 'customer' 
WHERE role IS NULL;
