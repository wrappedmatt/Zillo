package com.stripe.aod.sampleapp.fragment

import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.util.Log
import android.view.View
import androidx.fragment.app.Fragment
import androidx.navigation.fragment.navArgs
import com.google.zxing.BarcodeFormat
import com.google.zxing.WriterException
import com.google.zxing.qrcode.QRCodeWriter
import com.lemonade.terminal.loyalty.service.BrandingService
import com.stripe.aod.sampleapp.Config
import com.stripe.aod.sampleapp.R
import com.stripe.aod.sampleapp.databinding.FragmentQrCodeBinding
import com.stripe.aod.sampleapp.network.ApiClient
import com.stripe.aod.sampleapp.utils.backToHome

class QRCodeFragment : Fragment(R.layout.fragment_qr_code) {
    private val args: QRCodeFragmentArgs by navArgs()

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        val viewBinding = FragmentQrCodeBinding.bind(view)

        // Get branding settings
        val branding = BrandingService.getCurrentBranding()

        // Apply branding
        applyBranding(viewBinding, branding)

        // Get signup URL from arguments (passed from capture API response)
        val signupUrlPath = args.signupUrl
        val unclaimedPoints = args.unclaimedPoints

        Log.d(Config.TAG, "QR Code screen - Signup URL: $signupUrlPath, Unclaimed Points: $unclaimedPoints")

        // Construct full signup URL using backend API URL
        val baseUrl = ApiClient.backendUrl.removeSuffix("/")
        val fullSignupUrl = if (signupUrlPath.isNotEmpty()) {
            "$baseUrl$signupUrlPath"
        } else {
            // Fallback to branding signup URL if none provided
            "$baseUrl${branding.signupUrl}"
        }

        Log.d(Config.TAG, "QR Code full URL: $fullSignupUrl (baseUrl: $baseUrl, signupUrlPath: $signupUrlPath)")

        // Generate and display the QR code
        try {
            val bitmap = generateQRCode(fullSignupUrl, 512, 512)
            viewBinding.qrCodeImage.setImageBitmap(bitmap)
        } catch (e: WriterException) {
            e.printStackTrace()
            // Handle error - could show a message to user
        }

        // Handle button click - return to home
        viewBinding.doneButton.setOnClickListener {
            backToHome()
        }
    }

    private fun applyBranding(viewBinding: FragmentQrCodeBinding, branding: com.lemonade.terminal.loyalty.data.BrandingSettings) {
        // Apply background color
        viewBinding.root.setBackgroundColor(
            BrandingService.parseColor(branding.backgroundColor)
        )

        // Apply text colors
        val textColor = BrandingService.parseColor(branding.textColor)
        viewBinding.qrHeadline.apply {
            text = branding.qrHeadlineText
            setTextColor(textColor)
        }
        viewBinding.qrSubheadline.apply {
            text = branding.qrSubheadlineText
            setTextColor(textColor)
        }

        // Apply button colors
        val buttonColor = BrandingService.parseColor(branding.buttonColor)
        val buttonTextColor = BrandingService.parseColor(branding.buttonTextColor)

        viewBinding.doneButton.apply {
            text = branding.qrButtonText
            setTextColor(buttonTextColor)
            background = GradientDrawable().apply {
                setColor(buttonColor)
                cornerRadius = 8f * resources.displayMetrics.density
            }
        }
    }

    /**
     * Generate a QR code bitmap from a text string
     */
    private fun generateQRCode(text: String, width: Int, height: Int): Bitmap {
        val writer = QRCodeWriter()
        val bitMatrix = writer.encode(text, BarcodeFormat.QR_CODE, width, height)

        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.RGB_565)
        for (x in 0 until width) {
            for (y in 0 until height) {
                bitmap.setPixel(x, y, if (bitMatrix[x, y]) Color.BLACK else Color.WHITE)
            }
        }
        return bitmap
    }
}
