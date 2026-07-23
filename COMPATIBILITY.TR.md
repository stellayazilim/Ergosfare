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
>    işaretlendikleri sürümden sonraki minor'da (bölüm 4).

## 1. Kapsam

Bu politika tüm Ergosfare paketlerinin desteklenen public yüzeyini kapsar: mesaj ve
handler kontratları, mediator facade'ları, interceptor'lar, mediation stratejileri ve
modül kayıt API'leri.

## 2. Yüzey katmanları

| Katman | Ne | Vaat |
|--------|-----|------|
| **Stable** | Modül paketlerinin (Commands, Queries, Events, Contracts) public API'leri ve belgelenmiş kayıt/dispatch yüzeyi | Bölüm 3–4 kapsamında |
| **İç yüzey** | `Stella.Ergosfare.Core` / `Stella.Ergosfare.Core.Abstractions` implementasyon mekanizması — yalnızca birinci parti modüller assembly sınırları arasında tüketebilsin diye public | **Vaat yok.** Herhangi bir sürümde değişebilir; üçüncü parti eklenti kontratı değildir |
| **Deneysel** | `[Experimental]` işaretli API'ler (`ERGOEXP` önekli tanı kimlikleri) | **Vaat yok.** Herhangi bir sürümde değişebilir veya kaldırılabilir; tanı bastırılmadan tüketmek derleme hatasıdır — geçiş her zaman bilinçlidir |

## 3. Sürümleme kuralları

1. **Major sürümler (`vX.0.0`)** her şeyi değiştirebilir. **Major geçişler uyumluluk
   vaadinin tamamen dışındadır.** Major sürüm yeni bir hattır: bilinçli geçin ya da önceki
   hatta kalın — önceki hatlar bakımda kaldıkları sürece düzeltme almaya devam eder ve
   birinde kalmak tamamen desteklenen bir tercihtir.
2. **Minor sürümler (`vX.Y.0`)** özellik ve iyileştirme ekler. Sağlıklı, obsolete olmayan
   stable API'leri kırmaz — ancak (a) **kusurlu** API'leri düzeltebilir/kaldırabilir ve
   (b) daha önceki bir minor'da `[Obsolete]` işaretlenmiş API'leri kaldırabilir.
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

**Sağlıklı ama yerini yenisine bırakan API'ler** deprecation yaşam döngüsünü izler:

1. API `[Obsolete]` işaretlenir; attribute mesajı her zaman yeni adresi söyler.
2. Kendi minor hattı boyunca yerinde ve çalışır kalır — **patch'ler asla API kaldırmaz**.
3. **Kaldırılabileceği en erken nokta bir sonraki minor sürümdür**; sonraki herhangi bir
   minor veya major da kaldırabilir. Zamana dayalı bir pencere yoktur — obsolete bir API
   daha uzun da yaşayabilir, ama yaşamayacakmış gibi plan yapın.

Sözleşme derleyicidir: kusurlu-API istisnası dışında, **bugün uyarısız derlenen bir proje
tüm patch güncellemelerini ve bir sonraki minor sürümü sorunsuz atlatır.**

## 5. Deneysel (Experimental) API'ler

* `[Experimental]` işaretli API'ler (`ERGOEXP` önekli tanı kimlikleri, ör. `ERGOEXP001`)
  bu politikanın **tamamen dışındadır** — stable bir sürümde yayınlansalar bile.
* Obsolete adımı olmaksızın **herhangi bir** sürümde değiştirilebilir veya kaldırılabilir.
* Deneysel bir API'yi kullanmak, tüketici ilgili tanı kimliğini açıkça bastırmadıkça
  (ör. `#pragma warning disable ERGOEXP001` veya `<NoWarn>`) **derleme hatasıdır**.
* Deneysel bir API, attribute'un stable bir sürümde kaldırılmasıyla mezun olur; o sürümden
  itibaren bu politika kapsamında stable bir API'dir.
