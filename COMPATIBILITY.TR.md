# Ergosfare Sürümleme ve Uyumluluk Politikası

Ergosfare, bug-for-bug uyumluluk yerine **doğruluğu ve geliştirme hızını** önceler. Bu
belge, tam olarak neye güvenebileceğinizi — ve neye güvenemeyeceğinizi — söyler.

> **SemVer sapmaları, en baştan beyan.** Ergosfare SemVer tarzı sürüm numaraları kullanır
> ancak katı SemVer'den bilinçli olarak iki noktada sapar:
>
> 1. **Kusurlu API'ler herhangi bir sürümde, obsolete adımı olmadan düzeltilebilir veya
>    kaldırılabilir.** Yalnızca bir bug sayesinde var olan davranış, sözleşmenin parçası
>    değildir.
> 2. **`[Obsolete]` işaretli API'ler minor bir sürümde kaldırılabilir** — en erken,
>    işaretlendikleri sürümden sonraki minor’da veya deprecated işaretlendikleri/kaldırıldıkları preview’ın kararlı minor sürümünde (bölüm 4).

## 1. Kapsam

Bu politika tüm Ergosfare paketlerinin desteklenen public yüzeyini kapsar: mesaj ve
handler kontratları, mediator facade'ları, interceptor'lar, mediation stratejileri ve
modül kayıt API'leri.

## 2. Yüzey katmanları

| Katman | Ne | Vaat |
|--------|-----|------|
| **Stable** | Modül paketlerinin (Commands, Queries, Events, Contracts) public API'leri ve belgelenmiş kayıt/dispatch yüzeyi | Bölüm 3–4 kapsamında |
| **İç yüzey** | `Stella.Ergosfare.Core` / `Stella.Ergosfare.Core.Abstractions` implementasyon mekanizması — yalnızca birinci parti modüller assembly sınırları arasında tüketebilsin diye public | **Vaat yok.** Herhangi bir sürümde değişebilir; üçüncü parti eklenti kontratı değildir |
| **Deneysel** | `ERGOEXP` tanıları taşıyan deneysel API’ler (`ERGOEXP` önekli tanı kimlikleri) | **Vaat yok.** Herhangi bir sürümde değişebilir veya kaldırılabilir; kullanımları varsayılan olarak uyarı üretir |

## 3. Sürümleme kuralları

1. **Major sürümler (`vX.0.0`)** her şeyi değiştirebilir. **Major geçişler uyumluluk
   vaadinin tamamen dışındadır.** Major sürüm yeni bir hattır: bilinçli geçin ya da önceki
   hatta kalın — önceki hatlar bakımda kaldıkları sürece düzeltme almaya devam eder ve
   birinde kalmak tamamen desteklenen bir tercihtir.
2. **Minor sürümler (`vX.Y.0`)** özellik ve iyileştirme ekler. Sağlıklı, obsolete olmayan
   stable API'leri kırmaz — ancak (a) **kusurlu** API'leri düzeltebilir/kaldırabilir ve
   (b) önceki bir minor’da veya ilgili kararlı minor’ın preview’ında deprecated işaretlenen/kaldırılan API’leri kaldırabilir.
3. **Patch sürümleri (`vX.Y.Z`)** yalnızca düzeltme içerir — kusurlu davranışı değiştiren
   düzeltmeler dahil. **Patch'ler asla API kaldırmaz.**
4. **Ön sürümler (`vX.Y.Z-preview.N`)** hiçbir vaat taşımaz; ardışık iki preview arasında
   dahi.

Sürümler takvimle değil API değişiklikleriyle sürülür: kırıcı bir değişiklik yayınlamaya
değdiği anda major sürüm çıkar.

## 4. API yaşam döngüsü

**Kusurlu API'ler.** Yanlış çalışan, güvensiz olan ya da kendi belgelenmiş sözleşmesini
yerine getiremeyen bir API, **herhangi bir sürümde, obsolete adımı olmadan, derhal**
düzeltilebilir veya kaldırılabilir. Doğruluk uyumluluğu döver; bug-for-bug uyumluluk asla
korunmaz.

**Sağlıklı ama yerini yenisine bırakan API’ler.** Deprecation mesajı yeni API’yi veya geçiş yolunu belirtir. Preview’da deprecated işaretlenen veya kaldırılan bir API, ilgili kararlı minor sürümde doğrudan kaldırılabilir; önceki bir kararlı sürümde `[Obsolete]` işaretlenmiş olması gerekmez. Ayrı bir kararlı deprecation sürümü zorunlu değildir. Önceki kararlı minor’da deprecated işaretlenen API’ler de sonraki minor’da kaldırılabilir. Patch sürümleri sağlıklı API’leri kaldırmaz.

Önceki kararlı sürümde uyarısız derlenmek, bir sonraki minor sürümle uyumluluk garantisi vermez. Güncellemeden önce preview geçiş notlarını inceleyin.

## 5. Deneysel (Experimental) API’ler

* `ERGOEXP001`, `ERGOEXP002` veya `ERGOEXP003` tanıları taşıyan deneysel API’ler, kararlı sürümlerde de bu politikanın **tamamen dışındadır**.
* Deprecation süresi olmadan herhangi bir sürümde değişebilir veya kaldırılabilirler.
* `[Obsolete(..., false, DiagnosticId = "ERGOEXP...")]` ile varsayılan olarak **uyarı** üretirler. Mesaj kullanımdan kalkmış bir API’yi değil, deneysel bir yüzeyi belirtir. IDE yine de obsolete görünümü uygulayabilir.
* Mevcut `#pragma warning disable ERGOEXP001` ve `<NoWarn>` bastırmaları geçerlidir. Uyarıları hataya yükselten projeler ilgili tanıyı kendileri bastırmalı veya seviyesini düşürmelidir.
* Deneysel işaretin kararlı bir sürümde kaldırılmasıyla API kararlı sözleşmeye dahil olur.
