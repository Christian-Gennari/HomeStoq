/**
 * HomeStoq Settings Feature
 * Collapsible INI configuration editor with syntax highlighting
 */

function createSettingsFeature() {
    return {
        // State
        currentConfig: '',
        futureConfig: '',
        hasFutureConfig: false,
        isLoading: false,
        isSaving: false,
        lastModified: null,
        currentConfigExpanded: false,

        // Initialize
        async initSettings() {
            await this.loadConfigs();
        },

        // Load both configs
        async loadConfigs() {
            this.isLoading = true;

            try {
                const currentRes = await fetch('/api/config/current');
                if (currentRes.ok) {
                    this.currentConfig = await currentRes.text();
                } else {
                    this.currentConfig = '# config.ini not found';
                }

                const futureRes = await fetch('/api/config/future');
                if (futureRes.ok) {
                    this.futureConfig = await futureRes.text();
                    this.hasFutureConfig = true;

                    const statusRes = await fetch('/api/config/status');
                    if (statusRes.ok) {
                        const status = await statusRes.json();
                        if (status.modifiedAt) {
                            this.lastModified = new Date(status.modifiedAt);
                        }
                    }
                } else {
                    this.futureConfig = this.generateTemplate();
                    this.hasFutureConfig = false;
                    this.lastModified = null;
                }
            } catch (error) {
                console.error('Failed to load configs:', error);
                this.addToast('Failed to load configuration', 'error');
            } finally {
                this.isLoading = false;
            }
        },

        // Toggle current config panel open/closed
        toggleCurrentConfig() {
            this.currentConfigExpanded = !this.currentConfigExpanded;
        },

        // INI syntax highlighter — returns HTML string safe to use with x-html
        highlightIni(text) {
            if (!text) return '';

            const esc = (s) =>
                s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

            return text
                .split('\n')
                .map((raw) => {
                    const line = raw;
                    const trimmed = line.trim();

                    // Empty line
                    if (trimmed === '') return '';

                    // Full-line comment (# or ;)
                    if (trimmed.startsWith('#') || trimmed.startsWith(';')) {
                        return `<span class="ini-comment">${esc(line)}</span>`;
                    }

                    // Section header [SectionName]
                    const sectionMatch = trimmed.match(/^(\[)([^\]]+)(\])(.*)$/);
                    if (sectionMatch) {
                        const indent = line.match(/^(\s*)/)[1];
                        const trailing = sectionMatch[4]; // anything after ]
                        let out = esc(indent) +
                            '[' +
                            `<span class="ini-section">${esc(sectionMatch[2])}</span>` +
                            ']';
                        if (trailing.trim().startsWith('#') || trailing.trim().startsWith(';')) {
                            out += `<span class="ini-comment">${esc(trailing)}</span>`;
                        } else {
                            out += esc(trailing);
                        }
                        return out;
                    }

                    // Key=Value line
                    const eqIdx = line.indexOf('=');
                    if (eqIdx !== -1) {
                        const rawKey = line.slice(0, eqIdx);
                        const rawVal = line.slice(eqIdx + 1);

                        // Split inline comment off the value ( # ... or ; ...)
                        // Only split on ' #' or '  #' (space before #), not on bare #
                        const inlineMatch = rawVal.match(/^(.*?)\s{1,}(#.*)$/);
                        let valPart, commentPart;
                        if (inlineMatch) {
                            valPart = inlineMatch[1];
                            commentPart = rawVal.slice(valPart.length); // includes the spaces + #
                        } else {
                            valPart = rawVal;
                            commentPart = '';
                        }

                        return (
                            `<span class="ini-key">${esc(rawKey)}</span>` +
                            `<span class="ini-eq">=</span>` +
                            `<span class="ini-value">${esc(valPart)}</span>` +
                            (commentPart ? `<span class="ini-comment">${esc(commentPart)}</span>` : '')
                        );
                    }

                    // Fallback — plain escaped text
                    return esc(line);
                })
                .join('\n');
        },

        // Generate template from current config
        generateTemplate() {
            return `# Copy sections you want to change from the reference above
# Only include values you want to override
# This file takes precedence over config.ini on restart

# Example - uncomment and modify:
# [GoogleKeepScraper]
# KeepListName=inköpslistan, Shopping
# PollIntervalSeconds=60
# ActiveHours=08-22

# [AI]
# Provider=OpenRouter
`;
        },

        // Save future config
        async saveFutureConfig() {
            this.isSaving = true;

            try {
                const response = await fetch('/api/config/future', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(this.futureConfig)
                });

                if (response.ok) {
                    this.hasFutureConfig = true;
                    this.lastModified = new Date();
                    this.addToast(this.t('settings.saved'), 'success');
                } else if (response.status === 400) {
                    this.addToast(this.t('settings.invalidFormat'), 'error');
                } else {
                    throw new Error('Save failed');
                }
            } catch (error) {
                console.error('Failed to save:', error);
                this.addToast(this.t('settings.saveError'), 'error');
            } finally {
                this.isSaving = false;
            }
        },

        // Reset to defaults (delete future config)
        async resetToDefaults() {
            if (!confirm(this.t('settings.resetConfirm'))) return;

            this.isSaving = true;

            try {
                const response = await fetch('/api/config/future', { method: 'DELETE' });

                if (response.ok) {
                    this.futureConfig = this.generateTemplate();
                    this.hasFutureConfig = false;
                    this.lastModified = null;
                    this.addToast(this.t('settings.reset'), 'info');
                } else {
                    throw new Error('Reset failed');
                }
            } catch (error) {
                console.error('Failed to reset:', error);
                this.addToast(this.t('settings.resetError'), 'error');
            } finally {
                this.isSaving = false;
            }
        },

        // Format last modified date
        formatModifiedDate() {
            if (!this.lastModified) return '';
            return this.lastModified.toLocaleString(this.getLocale(), {
                month: 'short',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        },

        // Copy current config to clipboard
        async copyCurrentConfig() {
            const text = this.currentConfig;

            if (navigator.clipboard && navigator.clipboard.writeText) {
                try {
                    await navigator.clipboard.writeText(text);
                    this.addToast(this.t('settings.copied'), 'success');
                    return;
                } catch (_) {
                    // fall through to legacy method
                }
            }

            try {
                const ta = document.createElement('textarea');
                ta.value = text;
                ta.style.cssText = 'position:fixed;top:-9999px;left:-9999px;opacity:0';
                document.body.appendChild(ta);
                ta.focus();
                ta.select();
                const ok = document.execCommand('copy');
                document.body.removeChild(ta);
                if (ok) {
                    this.addToast(this.t('settings.copied'), 'success');
                } else {
                    throw new Error('execCommand returned false');
                }
            } catch (err) {
                console.error('Copy failed:', err);
                this.addToast(this.t('settings.copyError'), 'error');
            }
        }
    };
}

window.createSettingsFeature = createSettingsFeature;
