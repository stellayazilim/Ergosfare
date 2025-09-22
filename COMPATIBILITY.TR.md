# Ergosfare Sürümleme ve Geriye Dönük Uyumluluk Politikası


Belge, sürümlerin nasıl artırılacağını, geriye dönük uyumluluğun nasıl korunacağını ve obsolete API’lerin nasıl yönetileceğini tanımlar.

## 1. Kapsam

Bu belge, tüm Ergosfare Yüzey API’leri için geçerlidir. Bunlar arasında şunlar yer alır:

* Ergosfare modulleri ve Paketleri
* Mesajlar ve Handler kontratları 
* Mediation stratejileri
* Snapshot ve önbellekleme mekanizmaları

## 2. Sürümleme Stratejisi

1. **Major sürümler (`vX.0.0`)**

    * Breaking change’ler veya kaldırılabilecek obsolete API’leri içerir.
    * Önceki stable API’ler için geriye dönük uyumluluk, yalnızca obsolete olarak işaretlendikten sonraki **3 aylık süre** için garanti edilir.

2. **Minor sürümler (`vX.Y.Z`)**

    * Yeni özellikler veya iyileştirmeler ekler.
    * Stable API’lerde **breaking change yapamaz**. Obsolete API’ler çalışmaya devam eder.

3. **Patch sürümleri (`vX.Y.Z+`)**

    * Hata düzeltmeleri, performans iyileştirmeleri ve güvenlik yamaları içerir.
    * Yeni API eklemez veya mevcut API’leri kaldırmaz.

**Not:** Ergosfare, katı bir roadmap bazlı sürüm takvimi takip etmez. Yayın türü **API değişikliklerine göre** belirlenir: breaking change varsa, bir sonraki major sürüm hemen yayınlanabilir.

## 3. Geriye Dönük Uyumluluk

### 3.1 Tanımlar

* **Stable API’ler**: Stable bir sürüm (`vX.Y.Z`) içinde yayınlanmış public tipler, metodlar ve özellikler.
* **Obsolete API’ler**: Stable sürümde obsolete olarak işaretlenen API’ler. Bu işaretleme ile **3 ay olacak şekilde geriye dönük uyumluluk süresi** başlar.
* **Preview/RC API’ler**: Önizleme (preview) veya release candidate (RC) sürümlerinde yayınlanıp henüz stable sürümde yayınlanmadan önce obsolete olan API’ler için **geri uyumluluk garantisi yoktur**.

### 3.2 Obsolete API Yaşam Döngüsü

1. Yeni bir API tanıtıldığında veya mevcut API değiştirildiğinde, eski API stable sürümde **obsolete** olarak işaretlenebilir.
  
2. Stable sürümde obsolete olarak işaretlendiğinde:

    * API, **geriye dönük uyumluluk süresi boyunca tamamen işlevsel** kalır.
    * Geliştiriciler, **yeni API’ye geçmeleri** konusunda bilgilendirilir.
    * 3 aylık süre, obsolete API’nin güncel alternatifi ile birlikte stable sürümde yayınlandığı tarihten itibaren başlar.
3. Geriye dönük uyumluluk süresinin bitiminden sonra:

    * Obsolete API **bir sonraki major sürümde** kaldırılabilir.
    * Bu 3 aylık süre içinde birden fazla major, minor veya patch sürüm yayınlanabilir; kaldırma, sürenin dolması ile sınırlıdır.

### 3.3 İstisnalar

* Preview veya RC sürümünde yayınlanıp stable sürümden önce obsolete olan API’ler için **geri uyumluluk garantisi yoktur**.
* Güvenlik yamaları, 3 aylık süre dolmadan API’nin değiştirilmesini gerektirebilir; bu durumda **yeni API’ye geçiş şiddetle önerilir**.

### 3.4 Örnek

* `v1.0.0` stable sürüm olarak yayınlandı.
* `v1.4.0` sürümünde `OldMethod()` obsolete olarak işaretlendi.
* Geriye dönük uyumluluk süresi `v1.4.0`’dan itibaren başlar.
* Bu 3 aylık süre boyunca, v1.4.x veya v1.5.x gibi stable sürümlerde `OldMethod()` çalışmaya devam etmelidir.
* 3 ayın sonunda, bir sonraki major sürüm (`v2.0.0` veya daha sonrası) obsolete API’yi kaldırabilir.

