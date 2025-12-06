import { useState, useEffect } from 'react';
import { useParams, useSearchParams, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Loader2, Gift, TrendingUp, CheckCircle2 } from 'lucide-react';
import PhoneInput from 'react-phone-number-input';
import 'react-phone-number-input/style.css';

export default function CustomerSignup() {
  const { slug } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const fingerprint = searchParams.get('fingerprint');

  const [preview, setPreview] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  const [formData, setFormData] = useState({
    name: '',
    email: '',
    phone: ''
  });

  const [termsAccepted, setTermsAccepted] = useState(false);

  const [userCountry, setUserCountry] = useState('US');

  // Detect user's country from timezone
  useEffect(() => {
    try {
      const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;

      // Extract country from timezone (most timezones follow Continent/City format)
      // We'll use a comprehensive mapping for better accuracy
      const getCountryFromTimezone = (tz) => {
        // Direct timezone to country mapping for common timezones
        const timezoneMap = {
          // United States
          'America/New_York': 'US', 'America/Chicago': 'US', 'America/Denver': 'US',
          'America/Los_Angeles': 'US', 'America/Phoenix': 'US', 'America/Anchorage': 'US',
          'America/Honolulu': 'US', 'Pacific/Honolulu': 'US', 'America/Detroit': 'US',
          'America/Boise': 'US', 'America/Indianapolis': 'US',

          // Canada
          'America/Toronto': 'CA', 'America/Vancouver': 'CA', 'America/Montreal': 'CA',
          'America/Edmonton': 'CA', 'America/Halifax': 'CA', 'America/Winnipeg': 'CA',

          // United Kingdom
          'Europe/London': 'GB',

          // Europe
          'Europe/Paris': 'FR', 'Europe/Berlin': 'DE', 'Europe/Rome': 'IT',
          'Europe/Madrid': 'ES', 'Europe/Amsterdam': 'NL', 'Europe/Brussels': 'BE',
          'Europe/Vienna': 'AT', 'Europe/Stockholm': 'SE', 'Europe/Oslo': 'NO',
          'Europe/Copenhagen': 'DK', 'Europe/Helsinki': 'FI', 'Europe/Dublin': 'IE',
          'Europe/Lisbon': 'PT', 'Europe/Athens': 'GR', 'Europe/Warsaw': 'PL',
          'Europe/Prague': 'CZ', 'Europe/Budapest': 'HU', 'Europe/Bucharest': 'RO',
          'Europe/Zurich': 'CH', 'Europe/Kiev': 'UA', 'Europe/Moscow': 'RU',

          // Asia
          'Asia/Tokyo': 'JP', 'Asia/Shanghai': 'CN', 'Asia/Hong_Kong': 'HK',
          'Asia/Singapore': 'SG', 'Asia/Seoul': 'KR', 'Asia/Bangkok': 'TH',
          'Asia/Manila': 'PH', 'Asia/Kuala_Lumpur': 'MY', 'Asia/Jakarta': 'ID',
          'Asia/Taipei': 'TW', 'Asia/Dubai': 'AE', 'Asia/Tel_Aviv': 'IL',
          'Asia/Kolkata': 'IN', 'Asia/Karachi': 'PK', 'Asia/Dhaka': 'BD',
          'Asia/Ho_Chi_Minh': 'VN', 'Asia/Istanbul': 'TR',

          // Australia & Oceania
          'Australia/Sydney': 'AU', 'Australia/Melbourne': 'AU', 'Australia/Brisbane': 'AU',
          'Australia/Perth': 'AU', 'Australia/Adelaide': 'AU', 'Pacific/Auckland': 'NZ',

          // South America
          'America/Sao_Paulo': 'BR', 'America/Buenos_Aires': 'AR', 'America/Santiago': 'CL',
          'America/Lima': 'PE', 'America/Bogota': 'CO', 'America/Caracas': 'VE',

          // Mexico & Central America
          'America/Mexico_City': 'MX', 'America/Monterrey': 'MX', 'America/Cancun': 'MX',
          'America/Guatemala': 'GT', 'America/Panama': 'PA', 'America/Costa_Rica': 'CR',

          // Middle East
          'Asia/Riyadh': 'SA', 'Asia/Qatar': 'QA', 'Asia/Kuwait': 'KW',

          // Africa
          'Africa/Cairo': 'EG', 'Africa/Johannesburg': 'ZA', 'Africa/Lagos': 'NG',
          'Africa/Nairobi': 'KE', 'Africa/Casablanca': 'MA'
        };

        return timezoneMap[tz] || 'US';
      };

      const country = getCountryFromTimezone(timezone);
      setUserCountry(country);
      console.log(`Detected timezone: ${timezone}, country: ${country}`);
    } catch (error) {
      console.log('Could not detect country, defaulting to US');
      setUserCountry('US');
    }
  }, []);

  useEffect(() => {
    if (preview?.companyName) {
      document.title = `Sign Up | ${preview.companyName}`;
    } else {
      document.title = 'Sign Up | Loyalty Rewards';
    }
  }, [preview]);

  useEffect(() => {
    if (!fingerprint) {
      setError('Invalid signup link. Card fingerprint is required.');
      setLoading(false);
      return;
    }

    fetchPreview();
  }, [slug, fingerprint]);

  const fetchPreview = async () => {
    try {
      const response = await fetch(`/api/customer-portal/preview/${slug}?fingerprint=${fingerprint}`);

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Failed to load signup information');
      }

      const data = await response.json();
      setPreview(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);
    setError('');

    try {
      const response = await fetch(`/api/customer-portal/register/${slug}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          cardFingerprint: fingerprint,
          name: formData.name,
          email: formData.email || null,
          phone: formData.phone || null
        })
      });

      if (!response.ok) {
        const data = await response.json();
        throw new Error(data.error || 'Registration failed');
      }

      const data = await response.json();
      setSuccess(true);

      // Redirect to customer portal after 2 seconds
      setTimeout(() => {
        navigate(`/portal/${data.portalToken}`);
      }, 2000);
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  // Extract branding with defaults
  const branding = preview?.branding || {
    logoUrl: null,
    primaryColor: '#DC2626',
    backgroundColor: '#DC2626',
    textColor: '#FFFFFF',
    buttonColor: '#E5E7EB',
    buttonTextColor: '#1F2937'
  };

  const isCashback = preview?.loyaltySystemType === 'cashback';

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: `${branding.backgroundColor}15` }}>
        <Loader2 className="h-8 w-8 animate-spin" style={{ color: branding.primaryColor }} />
      </div>
    );
  }

  if (error && !preview) {
    return (
      <div className="min-h-screen flex items-center justify-center p-4" style={{ backgroundColor: `${branding.backgroundColor}15` }}>
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="text-red-600">Error</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (success) {
    return (
      <div className="min-h-screen flex items-center justify-center p-4" style={{ backgroundColor: `${branding.backgroundColor}15` }}>
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full" style={{ backgroundColor: `${branding.primaryColor}20` }}>
              <CheckCircle2 className="h-6 w-6" style={{ color: branding.primaryColor }} />
            </div>
            <CardTitle className="text-2xl">Welcome to {preview?.companyName}!</CardTitle>
            <CardDescription>
              Your account has been created successfully. Redirecting to your portal...
            </CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-white">
      {/* Top Bar */}
      <div
        className="w-full py-4 px-8 flex items-center justify-between shadow-sm"
        style={{ backgroundColor: branding.backgroundColor }}
      >
        <div className="flex items-center gap-3">
          {branding.logoUrl && (
            <img
              src={branding.logoUrl}
              alt={preview?.companyName}
              className="h-8 w-auto"
            />
          )}
          <h1
            className="text-xl font-semibold"
            style={{ color: branding.textColor }}
          >
            {preview?.companyName}
          </h1>
        </div>
      </div>

      <div className="grid lg:grid-cols-2 min-h-[calc(100vh-64px)]">
        {/* Left Column - Form */}
        <div className="flex flex-col justify-center px-8 py-12 lg:px-16">
          <div className="max-w-md mx-auto w-full">

            {/* Header */}
            <div className="mb-8">
              {isCashback ? (
                <>
                  <h1 className="text-5xl font-bold text-gray-900 mb-3">
                    ${((preview?.totalCashbackOnSignup || 0) / 100).toFixed(2)}
                  </h1>
                  <p className="text-xl text-gray-600 mb-2">
                    in cashback waiting for you
                  </p>
                  <p className="text-gray-500">
                    Join to claim your rewards and auto-earn on future purchases
                  </p>
                </>
              ) : (
                <>
                  <h1 className="text-5xl font-bold text-gray-900 mb-3">
                    {preview?.totalPointsOnSignup || 0} Points
                  </h1>
                  <p className="text-xl text-gray-600 mb-2">
                    waiting for you
                  </p>
                  <p className="text-gray-500">
                    Join to claim your rewards and auto-earn on future purchases
                  </p>
                </>
              )}
            </div>

            {/* Registration Form */}
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-4">
                <Input
                  type="text"
                  placeholder="First Name"
                  required
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  disabled={submitting}
                  className="h-12"
                />

                <Input
                  type="email"
                  placeholder="Email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  disabled={submitting}
                  className="h-12"
                />

                <PhoneInput
                  international
                  defaultCountry={userCountry}
                  placeholder="Enter phone number"
                  value={formData.phone}
                  onChange={(value) => setFormData({ ...formData, phone: value || '' })}
                  disabled={submitting}
                  className="h-12 flex items-center border border-input rounded-md px-3 py-2 text-sm ring-offset-background focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2"
                />
              </div>

              <div className="space-y-3 pt-4">
                <label className="flex items-start gap-2 text-sm text-gray-600">
                  <input
                    type="checkbox"
                    className="mt-0.5"
                    required
                    checked={termsAccepted}
                    onChange={(e) => setTermsAccepted(e.target.checked)}
                  />
                  <span>
                    By signing up, you agree to the{' '}
                    <a href="#" className="underline">Terms of Use</a> and{' '}
                    <a href="#" className="underline">Privacy Policy</a>.
                  </span>
                </label>

                <label className="flex items-start gap-2 text-sm text-gray-600">
                  <input type="checkbox" className="mt-0.5" />
                  <span>
                    Yes, I'd like to receive updates and promotions from {preview?.companyName}.
                  </span>
                </label>

                <div className="mt-4 pt-4 border-t border-gray-200">
                  <p className="text-sm text-gray-600">
                    The card you used will be linked to your account. Every time you use it at {preview?.companyName}, you'll automatically earn {isCashback ? 'cashback' : 'points'} — no need to scan or show anything.
                  </p>
                </div>
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Button
                type="submit"
                className="w-full h-12 text-base font-medium"
                disabled={submitting || !termsAccepted}
                style={{
                  backgroundColor: branding.backgroundColor,
                  color: branding.textColor
                }}
              >
                {submitting ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creating Account...
                  </>
                ) : (
                  <>
                    <CheckCircle2 className="mr-2 h-5 w-5" />
                    Join Loyalty Program
                  </>
                )}
              </Button>
            </form>
          </div>
        </div>

        {/* Right Column - Image */}
        <div
          className="hidden lg:block relative"
          style={{
            backgroundImage: branding.logoUrl ? `url(${branding.logoUrl})` : 'none',
            backgroundColor: `${branding.backgroundColor}10`,
            backgroundSize: 'cover',
            backgroundPosition: 'center'
          }}
        >
          {!branding.logoUrl && (
            <div className="absolute inset-0 flex items-center justify-center">
              <div className="text-center p-8">
                <h2 className="text-4xl font-bold mb-4" style={{ color: branding.primaryColor }}>
                  {preview?.companyName}
                </h2>
                <p className="text-xl text-gray-600">
                  Your {isCashback ? 'cashback' : 'loyalty'} rewards await
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
