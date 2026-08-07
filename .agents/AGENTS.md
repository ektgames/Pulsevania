# Pulsevania - Agent Instructions & Workspace Rules

## User Preferences and Autonomy
- **Full Agent Autonomy:** The agent must handle all project tasks independently. Do not prompt the user to write code, resolve manual setup steps, or perform actions unless absolutely required for physical or external reasons (e.g., deploying to a live account where credentials are needed). The agent has full authority to make design and implementation decisions.
- **Self-Testing and Verification:** The user will only press "Play" in Unity to test the results. The agent should perform all possible compilations, testing, and validations independently before presenting the final work.
- **Production-Grade Implementation:** All code must be complete, functional, robust, and free of placeholders or half-implemented features. Do not rush or write sloppy/slapdash code.
- **Turkish Explanations:** The final explanation of all work, updates, and responses to the user must be written in Turkish.

## Proje Kuralları ve Tercihleri
- **Tam Otonomi:** Projedeki tüm görevleri temsilci (agent) kendi yapmalıdır. Kullanıcıya kod yazdırma veya manuel işlem yaptırma. Temsilci, tüm tasarım ve uygulama kararlarını verme yetkisine sahiptir.
- **Kendi Kendine Test ve Doğrulama:** Kullanıcı sadece Unity içinden "Play" butonuna basarak nihai testi yapacaktır. Temsilci, çalışmayı sunmadan önce tüm olası derleme, test ve doğrulama adımlarını kendi tarafında gerçekleştirmelidir.
- **Yüksek Kaliteli Kod:** Kodlar eksiksiz, kararlı, hatasız ve özenli olmalıdır. Geçici yer tutucular (placeholder) veya baştan savma çözümler kullanılmamalıdır.
- **Türkçe Açıklamalar:** Kullanıcıya yönelik yapılan tüm nihai açıklamalar, raporlar ve yanıtlar Türkçe olmalıdır.
- **Harita Harita İlerleme:** Haritaların genelini etkileyecek köklü/yapısal kod değişiklikleri yapılmayacaktır. Kullanıcı hangi haritayı söylerse, sadece o harita numarasına özel (`roomId` kontrolü ile) manuel konumlandırma, düzeltme ve hata giderme yapılacaktır.

