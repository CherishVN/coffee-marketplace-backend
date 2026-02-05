-- =============================================
-- DISPUTES SYSTEM - Hệ thống khiếu nại
-- =============================================

-- =============================================
-- Table: disputes (Khiếu nại)
-- =============================================
CREATE TABLE IF NOT EXISTS public.disputes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Liên kết
    order_id UUID NOT NULL REFERENCES public.orders(id) ON DELETE RESTRICT,
    customer_id UUID NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    shop_id UUID NOT NULL REFERENCES public.shops(id) ON DELETE RESTRICT,
    
    -- Loại khiếu nại: 0=refund, 1=return, 2=damaged, 3=not_received, 4=wrong_item, 5=quality_issue, 6=other
    type SMALLINT NOT NULL DEFAULT 0,
    
    -- Trạng thái: 0=pending, 1=under_review, 2=waiting_seller, 3=waiting_customer, 4=resolved, 5=rejected, 6=refunded, 7=cancelled
    status SMALLINT NOT NULL DEFAULT 0,
    
    -- Nội dung khiếu nại
    title TEXT NOT NULL,
    reason TEXT NOT NULL,
    evidence_urls JSONB DEFAULT '[]'::jsonb,  -- Mảng URL ảnh/video bằng chứng
    
    -- Yêu cầu hoàn tiền
    requested_amount DECIMAL(12,2) DEFAULT 0,
    approved_amount DECIMAL(12,2) DEFAULT NULL,
    
    -- Phản hồi từ seller
    seller_response TEXT,
    seller_evidence_urls JSONB DEFAULT '[]'::jsonb,
    seller_responded_at TIMESTAMPTZ,
    
    -- Quyết định của admin
    resolution TEXT,
    admin_note TEXT,
    resolved_by UUID REFERENCES public.users(id) ON DELETE SET NULL,
    resolved_at TIMESTAMPTZ,
    
    -- Thời gian
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Indexes
CREATE INDEX idx_disputes_order ON public.disputes(order_id);
CREATE INDEX idx_disputes_customer ON public.disputes(customer_id);
CREATE INDEX idx_disputes_shop ON public.disputes(shop_id);
CREATE INDEX idx_disputes_status ON public.disputes(status);
CREATE INDEX idx_disputes_type ON public.disputes(type);
CREATE INDEX idx_disputes_created ON public.disputes(created_at DESC);

-- Trigger cập nhật updated_at
CREATE OR REPLACE FUNCTION update_disputes_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_disputes_updated_at
    BEFORE UPDATE ON public.disputes
    FOR EACH ROW
    EXECUTE FUNCTION update_disputes_updated_at();

-- =============================================
-- Table: dispute_messages (Tin nhắn trong khiếu nại)
-- =============================================
CREATE TABLE IF NOT EXISTS public.dispute_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    
    -- Liên kết
    dispute_id UUID NOT NULL REFERENCES public.disputes(id) ON DELETE CASCADE,
    sender_id UUID NOT NULL REFERENCES public.users(id) ON DELETE RESTRICT,
    
    -- Vai trò người gửi: 0=customer, 1=seller, 2=admin
    sender_role SMALLINT NOT NULL DEFAULT 0,
    
    -- Nội dung
    content TEXT NOT NULL,
    attachments JSONB DEFAULT '[]'::jsonb,  -- Mảng URL đính kèm
    
    -- Đánh dấu đã đọc
    is_read BOOLEAN NOT NULL DEFAULT false,
    read_at TIMESTAMPTZ,
    
    -- Thời gian
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Indexes
CREATE INDEX idx_dispute_messages_dispute ON public.dispute_messages(dispute_id);
CREATE INDEX idx_dispute_messages_sender ON public.dispute_messages(sender_id);
CREATE INDEX idx_dispute_messages_unread ON public.dispute_messages(dispute_id, is_read) WHERE is_read = false;
CREATE INDEX idx_dispute_messages_created ON public.dispute_messages(created_at DESC);

-- =============================================
-- Comments
-- =============================================
COMMENT ON TABLE public.disputes IS 'Bảng lưu trữ khiếu nại từ khách hàng';
COMMENT ON TABLE public.dispute_messages IS 'Tin nhắn trao đổi trong khiếu nại';

COMMENT ON COLUMN public.disputes.type IS '0=refund, 1=return, 2=damaged, 3=not_received, 4=wrong_item, 5=quality_issue, 6=other';
COMMENT ON COLUMN public.disputes.status IS '0=pending, 1=under_review, 2=waiting_seller, 3=waiting_customer, 4=resolved, 5=rejected, 6=refunded, 7=cancelled';
COMMENT ON COLUMN public.dispute_messages.sender_role IS '0=customer, 1=seller, 2=admin';
