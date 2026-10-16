import { StyleSheet } from 'react-native';
// Legacy token names remain aliases so every workflow adopts the same quiet palette.
export const C = { forest: '#171B19', green: '#087A41', mint: '#087A41', lime: '#087A41', bg: '#F6F7F7', white: '#FFFFFF', ink: '#171B19', muted: '#606864', border: '#E4E8E6', pale: '#F0F4F2', amber: '#555E59', amberBg: '#F1F3F2', blue: '#087A41', blueBg: '#EDF7F1', red: '#B42332', redBg: '#FFF0F1' };
export const S = StyleSheet.create({
  text: { fontFamily: 'Inter', color: C.ink, fontSize: 14, lineHeight: 21 },
  muted: { fontFamily: 'Inter', color: C.muted, fontSize: 13, lineHeight: 20 },
  heading: { fontFamily: 'Inter', color: C.ink, fontSize: 27, lineHeight: 34, fontWeight: '600', letterSpacing: -0.65 },
  subheading: { fontFamily: 'Inter', color: C.ink, fontSize: 18, lineHeight: 25, fontWeight: '600', letterSpacing: -0.3 },
  label: { fontFamily: 'Inter', color: C.ink, fontSize: 13, lineHeight: 19, fontWeight: '600' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  between: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 },
  panel: { backgroundColor: C.white, borderRadius: 12, padding: 24, borderWidth: 1, borderColor: C.border },
  divider: { height: 1, backgroundColor: C.border },
  input: { backgroundColor: C.white, borderColor: '#A9B3AD', borderWidth: 1, borderRadius: 9, paddingHorizontal: 14, paddingVertical: 12, minHeight: 48, fontFamily: 'Inter', fontSize: 15, color: C.ink },
  button: { borderRadius: 9, minHeight: 48, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 18, paddingVertical: 12, flexDirection: 'row', gap: 8 },
  buttonText: { fontFamily: 'Inter', fontSize: 13, fontWeight: '600', lineHeight: 20 },
  small: { fontFamily: 'Inter', color: C.muted, fontSize: 12, lineHeight: 18 },
});
