-- Adicionar campos de notificação de status aos coworkings
-- Migration: 20260130120000_AddStatusNotificationToCoworkings

-- Adicionar campo HasPendingStatusNotification
ALTER TABLE "Coworkings" 
ADD COLUMN IF NOT EXISTS "HasPendingStatusNotification" boolean NOT NULL DEFAULT false;

-- Adicionar campo LastNotifiedStatus
ALTER TABLE "Coworkings" 
ADD COLUMN IF NOT EXISTS "LastNotifiedStatus" integer NULL;

-- Comentários
COMMENT ON COLUMN "Coworkings"."HasPendingStatusNotification" IS 'Indica se há uma notificação de mudança de status pendente para ser visualizada pelo parceiro';
COMMENT ON COLUMN "Coworkings"."LastNotifiedStatus" IS 'Último status notificado ao parceiro (0=UnderReview, 1=AwaitingAdjustment, 2=Approved)';
