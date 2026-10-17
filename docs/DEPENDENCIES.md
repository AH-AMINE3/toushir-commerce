# Dependency review · 17 October 2026

The desktop npm audit reported zero known advisories. The full NuGet dependency audit reported zero known vulnerable packages. These are published-advisory checks, not a penetration test or a guarantee about unknown vulnerabilities.

Expo/React Native's npm tree currently reports22 affected packages:15 high and7 moderate. The underlying leaf advisories are braces (nested-pattern stack exhaustion), node-forge (RSA signature verification), and uuid (buffer bounds). The parent Expo/Metro/Xcode packages inherit those findings. npm proposes incompatible major downgrades rather than a compatible patched stack; those downgrades were not applied.

Metro, prebuild, Xcode parsing and development certificate tooling are not exposed as public servers. The hosted app serves compiled bundles from .NET. This limits the affected build-tool exposure; it does not erase the advisories. The build should process only trusted project/configuration inputs. Recheck upstream compatible fixes before commercial release and retest native generation/signing after any override. Do not use forced package downgrades to report a cosmetically clean audit.

Package versions are pinned in npm and NuGet lockfiles. DM Sans's OFL license and catalogue source/image provenance are retained. Project-owned Figma assets use the supplied design; trademark and open-data rights remain with their owners.
