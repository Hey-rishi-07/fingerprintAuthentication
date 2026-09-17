<?php
/**
 * CentralMFA Calling Application Integration Sample - PHP
 *
 * Demonstrates:
 * 1. Post-LDAP Login MFA redirect with HMAC-SHA256 signature
 * 2. Sensitive Submit-Button / Step-Up Authorization redirect
 * 3. Secure callback verification using timing-safe hash comparison
 */

// Configuration - Registered in CentralMFA Admin
define('MFA_GATEWAY_URL', 'http://intranet-server/CentralMFA/Gateway.aspx');
define('APP_ID', 'HRMS');
define('APP_SECRET', 'HRMS_SECRET_KEY_8F9A2B1C');

$notice = null;
$noticeType = 'info';

// Check if user is returning from CentralMFA Gateway
if (isset($_GET['status'])) {
    $status    = $_GET['status'] ?? '';
    $appId     = $_GET['appId'] ?? '';
    $pcno      = $_GET['pcno'] ?? '';
    $action    = $_GET['action'] ?? 'LOGIN';
    $txnId     = $_GET['txnId'] ?? '';
    $timestamp = $_GET['timestamp'] ?? '';
    $signature = $_GET['sig'] ?? $_GET['signature'] ?? '';

    // 1. Verify HMAC-SHA256 Signature (Canonical format: appId|pcno|status|action|txnId|timestamp|nonce)
    $dataToVerify = "{$appId}|{$pcno}|{$status}|{$action}|{$txnId}|{$timestamp}|{$nonce}";
    $expectedSignature = hash_hmac('sha256', $dataToVerify, APP_SECRET);

    if (!hash_equals(strtolower($expectedSignature), strtolower($signature))) {
        $notice = '<strong>Security Error:</strong> Cryptographic signature mismatch! The response could not be verified.';
        $noticeType = 'danger';
    } else {
        // 2. Check Timestamp Freshness (120 seconds window)
        $isFresh = false;
        if (is_numeric($timestamp)) {
            $isFresh = abs(time() - (int)$timestamp) <= 120;
        } else {
            $respTime = DateTime::createFromFormat('YmdHis', $timestamp, new DateTimeZone('UTC'));
            $now = new DateTime('now', new DateTimeZone('UTC'));
            $isFresh = ($respTime && abs($now->getTimestamp() - $respTime->getTimestamp()) <= 120);
        }
        
        if (!$isFresh) {
            $notice = '<strong>Expired Token:</strong> The biometric verification response has expired. Please retry.';
            $noticeType = 'warning';
        } elseif (strcasecmp($status, 'APPROVED') === 0) {
            $noticeType = 'success';
            if (strcasecmp($action, 'LOGIN') === 0) {
                $notice = "<strong>Biometric MFA Succeeded!</strong> Employee PCNO <strong>" . htmlspecialchars($pcno) . "</strong> verified. User session started.";
            } else {
                $notice = "<strong>Transaction Authorized!</strong> Action: <strong>" . htmlspecialchars($action) . "</strong> | TxnRef: <strong>" . htmlspecialchars($txnId) . "</strong> | Verified PCNO: <strong>" . htmlspecialchars($pcno) . "</strong>.";
            }
        } else {
            $notice = "<strong>Biometric MFA Denied:</strong> Status: " . htmlspecialchars($status) . " for PCNO: " . htmlspecialchars($pcno);
            $noticeType = 'danger';
        }
    }
}

// Handle Form Submissions to initiate MFA
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $actionType = $_POST['action_type'] ?? 'LOGIN';
    $pcno       = trim($_POST['pcno'] ?? '');
    $txnId      = trim($_POST['txn_id'] ?? '');

    if (!empty($pcno)) {
        $protocol = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? "https://" : "http://";
        $currentUrl = $protocol . $_SERVER['HTTP_HOST'] . strtok($_SERVER["REQUEST_URI"], '?');
        
        $timestamp = gmdate('YmdHis');
        $nonce = bin2hex(openssl_random_pseudo_bytes(8));

        // Create HMAC Signature for request
        $dataToSign = APP_ID . '|' . $pcno . '|' . $currentUrl . '|' . $actionType . '|' . $txnId . '|' . $timestamp . '|' . $nonce;
        $signature = strtoupper(hash_hmac('sha256', $dataToSign, APP_SECRET));

        $params = http_build_query([
            'appId'     => APP_ID,
            'pcno'      => $pcno,
            'returnUrl' => $currentUrl,
            'action'    => $actionType,
            'txnId'     => $txnId,
            'timestamp' => $timestamp,
            'nonce'     => $nonce,
            'signature' => $signature
        ]);

        header("Location: " . MFA_GATEWAY_URL . "?" . $params);
        exit;
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Calling App Demo (PHP) - CentralMFA</title>
    <link href="../Static/app.css" rel="stylesheet" type="text/css">
</head>
<body style="background: #f1f5f9;">
    <header class="app-navbar" style="background: #0f172a;">
        <a href="#" class="brand">
            <span style="font-size: 1.15rem; font-weight: 700; color: #f8fafc;">HRMS Portal (Calling App Demo)</span>
            <span class="brand-badge" style="background: #334155; color: #94a3b8;">PHP</span>
        </a>
        <div style="font-size: 0.82rem; color: #94a3b8;">
            AppId: <strong>HRMS</strong>
        </div>
    </header>

    <main class="main-content" style="max-width: 860px; margin: 36px auto; padding: 0 16px;">
        <?php if ($notice): ?>
            <div class="alert alert-<?= $noticeType ?>" style="margin-bottom: 24px;">
                <?= $notice ?>
            </div>
        <?php endif; ?>

        <!-- Scenario 1: Post-LDAP Login MFA -->
        <div class="card" style="margin-bottom: 24px;">
            <div class="card-header">
                <h2 class="card-title">Scenario 1: Post-LDAP Login Biometric MFA (PHP)</h2>
                <p class="card-desc">
                    Employee passes LDAP authentication. PHP backend signs the request with HMAC-SHA256 and redirects user to the MFA Gateway.
                </p>
            </div>
            <form method="POST" action="">
                <input type="hidden" name="action_type" value="LOGIN">
                <div class="form-group">
                    <label class="form-label">Employee PCNO (from LDAP)</label>
                    <input type="text" name="pcno" class="form-control" value="EMP002" style="max-width: 320px;" required>
                </div>
                <button type="submit" class="btn btn-primary">Simulate LDAP Login &amp; Redirect to MFA Gateway</button>
            </form>
        </div>

        <!-- Scenario 2: Sensitive Button Click / Transaction Authorization -->
        <div class="card">
            <div class="card-header">
                <h2 class="card-title">Scenario 2: Sensitive Submit Button Authorization (PHP)</h2>
                <p class="card-desc">
                    Manager submits leave sanction or salary appraisal. Requires biometric authentication before executing PHP SQL transaction.
                </p>
            </div>
            <form method="POST" action="">
                <input type="hidden" name="action_type" value="SANCTION_LEAVE">
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px;">
                    <div class="form-group">
                        <label class="form-label">Transaction Reference (TxnId)</label>
                        <input type="text" name="txn_id" class="form-control" value="LV-2026-0842" required>
                    </div>
                    <div class="form-group">
                        <label class="form-label">Manager PCNO</label>
                        <input type="text" name="pcno" class="form-control" value="EMP002" required>
                    </div>
                </div>
                <button type="submit" class="btn btn-secondary">Sanction Leave with Fingerprint Authorization</button>
            </form>
        </div>
    </main>
</body>
</html>
