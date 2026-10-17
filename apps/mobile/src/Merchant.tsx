import React, { useEffect, useRef, useState } from 'react';
import { Alert, BackHandler, FlatList, KeyboardAvoidingView, Linking, Modal, Platform, Pressable, RefreshControl, ScrollView, Share, StyleSheet, Text, TextInput, useWindowDimensions, View } from 'react-native';
import {Image as FigmaImage} from 'expo-image';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { Address, api, API_ORIGIN, CartLine, CartQuote, date, money, operationKey, Order, OrderSummary, Page, post, Product, put, quantityLabel, remove, Session, statuses } from './api';
import { Button, Empty, Field, FilterPill, Icon, Loading, Logo, Notice, ProductImage, Status, Stepper } from './components';
import { C, S } from './theme';
import { MerchantClaims } from './Claims';
import {Campaigns,Conversations} from './Engagement';
import MerchantAccount, {AccountPanel, accountTitles} from './MerchantAccount';
import OrderGifts from './OrderGifts';
import MerchantDelivery from './MerchantDelivery';
import {merchantTab} from './runtime';

type Props = { session: Session; onJoin: () => void; onSwitch: () => Promise<void>; onLogout: () => Promise<void>; onNotice: (message: string) => void };
type Screen = 'catalog' | 'favorites' | 'cart' | 'orders' | 'account' | 'offers' | 'messages';
const nav: {id:Screen;label:string;icon:string}[]=[{id:'catalog',label:'Accueil',icon:'home-outline'},{id:'offers',label:'Offres',icon:'pricetag-outline'},{id:'orders',label:'Commandes',icon:'cube-outline'},{id:'messages',label:'Messages',icon:'chatbubble-outline'},{id:'account',label:'Mon Toushir',icon:'person-outline'}];
const categoryIcon = (name: string) => /boisson|eau|jus/i.test(name) ? 'water-outline' : /lait|fromage|laitier/i.test(name) ? 'cafe-outline' : /biscuit|sucr|chocolat|snack/i.test(name) ? 'ice-cream-outline' : /pâte|riz|semoule|céréal/i.test(name) ? 'restaurant-outline' : /conserve|sauce|épicerie/i.test(name) ? 'nutrition-outline' : 'basket-outline';
const toLines = (quote: CartQuote): CartLine[] => quote.items.map(item => ({ productId: item.product.id, quantity: item.quantity }));

export default function Merchant({ session, onJoin, onSwitch, onLogout, onNotice }: Props) {
  const { width } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const wide = width >= 900;
  const columns = width >= 1080 ? 4 : width >= 680 ? 3 : 2;
  const [screen, setScreen] = useState<Screen>(() => { const target = Platform.OS === 'web' ? window.location.hash.slice(1) : ''; return nav.some(n => n.id === target) ? target as Screen : 'catalog'; });
  const [communicationSettings,setCommunicationSettings]=useState(false);
  const [accountPanel,setAccountPanel] = useState<AccountPanel>(null);
  const contentRef=useRef<React.ElementRef<typeof ScrollView>>(null);
  useEffect(()=>{contentRef.current?.scrollTo({y:0,animated:false});},[accountPanel,screen]);
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<{ id: string; name: string; count: number }[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('');
  const [sort, setSort] = useState('featured');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [quote, setQuote] = useState<CartQuote | null>(null);
  const [addresses, setAddresses] = useState<Address[]>([]);
  const [addressId, setAddressId] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [cartSaving, setCartSaving] = useState(false);
  const [cartSaveError, setCartSaveError] = useState(false);
  const [locationOpen, setLocationOpen] = useState(false);
  const [sortOpen, setSortOpen] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [incomingOrder, setIncomingOrder] = useState<OrderSummary | null>(null);
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [orderPage, setOrderPage] = useState(1);
  const [orderPages, setOrderPages] = useState(1);
  const [order, setOrder] = useState<Order | null>(null);
  const [orderFilter, setOrderFilter] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [addressForm, setAddressForm] = useState<{ label: string; commune: string; street: string } | null>(null);
  const checkoutKey = useRef(operationKey());
  const saveVersion = useRef(0);
  const saveQueue = useRef(Promise.resolve());
  const booted = useRef(false);

  useEffect(() => { const timer = setTimeout(() => { setQuery(search.trim()); setPage(1); }, 300); return () => clearTimeout(timer); }, [search]);
  useEffect(() => {
    let active = true;
    Promise.all([api<CartQuote>('/cart'), api<Product[]>('/favorites'), api<Address[]>('/addresses'), api<any[]>('/categories')]).then(([basket, favorites, locations, cats]) => {
      if (!active) return;
      setCart(toLines(basket)); setQuote(basket); setFavoriteProducts(favorites); setAddresses(locations); setAddressId(locations[0]?.id || ''); setCategories(cats); booted.current = true;
    }).catch(e => active && setError(e.message));
    return () => { active = false; };
  }, [session.user.id]);
  useEffect(() => { let active = true; const load = () => api<Page<OrderSummary>>('/orders?page=1&pageSize=15').then(r => { if(active) setIncomingOrder(r.items.find(o => ['PREPARING','READY','OUT_FOR_DELIVERY'].includes(o.status)) || null); }).catch(() => {}); load(); const timer = setInterval(load,15000); return () => {active=false;clearInterval(timer);}; },[session.user.id,refresh]);
  useEffect(() => {
    if (screen !== 'catalog') return;
    let active = true;
    setLoading(true); setError('');
    api<Page<Product>>(`/products?page=${page}&pageSize=30&q=${encodeURIComponent(query)}&category=${encodeURIComponent(category)}&sort=${sort}`).then(result => {
      if (active) { setProducts(result.items); setTotal(result.total); setTotalPages(result.totalPages); }
    }).catch(e => active && setError(e.message)).finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [screen, query, category, page, sort, refresh]);
  useEffect(() => {
    if (screen !== 'orders') return;
    let active = true;
    const load = async () => {
      try { const result = await api<Page<OrderSummary>>(`/orders?page=${orderPage}&pageSize=15&status=${orderFilter}`); if (active) { setOrders(result.items); setOrderPages(result.totalPages); setLoading(false); } }
      catch (e: any) { if (active) { setError(e.message); setLoading(false); } }
    };
    setLoading(true); load(); const timer = setInterval(load, 15000);
    return () => { active = false; clearInterval(timer); };
  }, [screen, orderPage, orderFilter, refresh]);
  useEffect(() => {
    if (!order) return;
    let active = true;
    const timer = setInterval(() => api<Order>(`/orders/${order.id}`).then(value => { if (active) setOrder(value); }).catch(() => {}), 15000);
    return () => { active = false; clearInterval(timer); };
  }, [order?.id]);
  useEffect(() => {
    const listener = BackHandler.addEventListener('hardwareBackPress', () => { if (selectedProduct) { setSelectedProduct(null); return true; } if (order) { setOrder(null); return true; } if(accountPanel){setAccountPanel(null);return true;} if (screen !== 'catalog') { setScreen('catalog'); return true; } return false; });
    return () => listener.remove();
  }, [screen, selectedProduct, order, accountPanel]);
  const navigate = (target: Screen) => { setAccountPanel(null); setScreen(target); setError(''); setOrder(null); setSelectedProduct(null); merchantTab(target); };
  const favoriteIds = new Set(favoriteProducts.map(p => p.id));
  const cartCount = cart.reduce((sum, line) => sum + line.quantity, 0);
  const cartQuantity = (p: Product) => cart.find(line => line.productId === p.id)?.quantity || 0;
  const saveCart = (next: CartLine[]) => {
    setCart(next); setCartSaving(true); setCartSaveError(false); checkoutKey.current = operationKey();
    const version = ++saveVersion.current;
    saveQueue.current = saveQueue.current.catch(() => {}).then(async () => {
      try { const result = await put<CartQuote>('/cart', { items: next }); if (version === saveVersion.current) { setQuote(result); setCartSaveError(false); } }
      catch (e: any) { if (version === saveVersion.current) { setError(e.message); setCartSaveError(true); } }
      finally { if (version === saveVersion.current) setCartSaving(false); }
    });
  };
  function changeQuantity(product: Product, quantity: number) {
    setError('');
    const next = cart.filter(line => line.productId !== product.id);
    if (quantity > 0) next.push({ productId: product.id, quantity });
    saveCart(next);
  }
  async function toggleFavorite(product: Product) {
    try {
      if (favoriteIds.has(product.id)) { await remove(`/favorites/${product.id}`); setFavoriteProducts(old => old.filter(p => p.id !== product.id)); }
      else { await post(`/favorites/${product.id}`); setFavoriteProducts(old => [...old, product]); }
    } catch (e: any) { setError(e.message); }
  }
  async function checkout() {
    if (!addressId) { setError('Ajoutez une adresse de livraison avant d’envoyer votre demande.'); return; }
    setBusy(true); setError('');
    try {
      await saveQueue.current;
      if (cartSaveError) throw new Error('Votre panier n’est pas encore enregistré. Réessayez sa synchronisation avant de continuer.');
      const currentQuote = await api<CartQuote>('/cart');
      const result = await post<Order>('/orders', { idempotencyKey: checkoutKey.current, addressId, items: cart, note, mode: currentQuote.needsQuote ? 'REQUEST_QUOTE' : 'PLACE_ORDER' });
      ++saveVersion.current; setCart([]); setQuote(null); setCartSaving(false); setNote(''); checkoutKey.current = operationKey(); navigate('orders'); setOrder(result); setRefresh(n => n + 1); onNotice('Votre demande a bien été transmise au grossiste.');
    } catch (e: any) { setError(e.message); } finally { setBusy(false); }
  }
  async function orderAction(action: string, body: any = {}, message = 'Votre commande a été mise à jour.') {
    if (!order) return;
    setBusy(true); setError('');
    try {
      const result = await post<any>(`/orders/${order.id}/${action}`, body);
      if (action === 'reorder') { saveCart(toLines(result)); setOrder(null); navigate('cart'); }
      else { setOrder(result); setRefresh(n => n + 1); }
      onNotice(message);
    } catch (e: any) { setError(e.message); } finally { setBusy(false); }
  }
  async function saveAddress() {
    if (!addressForm) return;
    setBusy(true); setError('');
    try { const next = await post<Address>('/addresses', { ...addressForm, wilaya: 'Oran' }); setAddresses(old => [...old, next]); setAddressId(next.id); setAddressForm(null); onNotice('Adresse de livraison ajoutée.'); }
    catch (e: any) { setError(e.message); } finally { setBusy(false); }
  }
  function confirmCancel() {
    const run = () => orderAction('cancel', { reason: 'Annulation demandée par le commerçant.' }, 'Votre demande a été annulée.');
    if (Platform.OS === 'web') { if (window.confirm('Annuler cette demande ? Les quantités réservées seront libérées.')) run(); }
    else Alert.alert('Annuler cette demande ?', 'Les quantités réservées seront libérées.', [{ text: 'Conserver', style: 'cancel' }, { text: 'Annuler la demande', style: 'destructive', onPress: run }]);
  }
  const appWidth = Math.min(width, 520) - (Platform.OS === 'web' ? 2 : 0);
  const cardWidth = (appWidth - 52) / 2;
  const catalogRef = useRef<FlatList<Product>>(null);
  useEffect(() => { catalogRef.current?.scrollToOffset({ offset: 0, animated: false }); }, [page, query, category]);
  const selectedAddress = addresses.find(a => a.id === addressId) || addresses[0];
  const handleSwitch = () => onSwitch().catch(e => setError(e.message));
  const handleLogout = () => onLogout().catch(e => setError(e.message));
  const productCard = (product: Product) => <View style={[M.productCard, { width: cardWidth }]}>
    <View style={M.packshot}>
      <Pressable accessibilityRole="button" accessibilityLabel={`Voir ${product.name}`} onPress={() => setSelectedProduct(product)}><ProductImage uri={product.imageUrl} name={product.name} style={{ height: appWidth < 370 ? 126 : 145, aspectRatio: undefined }} /></Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel={`${favoriteIds.has(product.id) ? 'Retirer' : 'Ajouter'} ${product.name} ${favoriteIds.has(product.id) ? 'des' : 'aux'} favoris`} onPress={() => toggleFavorite(product)} style={({ pressed }) => [M.favorite, { opacity: pressed ? .6 : 1 }]}><Icon name={favoriteIds.has(product.id) ? 'heart' : 'heart-outline'} size={20} color={favoriteIds.has(product.id) ? C.green : C.ink} /></Pressable>
    </View>
    <View style={M.productBody}><Text style={M.brand} numberOfLines={1}>{product.brand || 'Épicerie'}</Text><Pressable accessibilityRole="button" onPress={() => setSelectedProduct(product)}><Text style={M.productName} numberOfLines={2}>{product.name}</Text></Pressable><Text style={M.pack} numberOfLines={1}>{quantityLabel(product)}</Text><View style={[S.between,{gap:6,alignItems:'center',marginTop:'auto',flexWrap:'wrap'}]}><Text style={[M.price,{flex:1,marginTop:0,marginBottom:0}]}>{money(product.price)}</Text>
      {cartQuantity(product) ? <QuantityControl value={cartQuantity(product)} minimum={product.minimumQuantity || 1} onChange={n => changeQuantity(product, n)} name={product.name} /> : <Pressable accessibilityRole="button" accessibilityLabel={`Ajouter ${product.name} au panier`} onPress={() => { changeQuantity(product, product.minimumQuantity || 1); onNotice('Ajouté au panier'); }} style={({ pressed }) => [M.addButton, { opacity: pressed ? .7 : 1 }]}><Icon name="add" size={22} color={C.white} /></Pressable>}</View>
    </View>
  </View>;
  const categoryStrip = <><View style={[S.between,{marginTop:20,marginBottom:16}]}><Text style={[M.sectionTitle,{fontSize:22,lineHeight:30}]}>Vos rayons</Text><Button variant="ghost" small onPress={() => {setCategory('');setPage(1);}}>Tout voir</Button></View><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={M.categoryRail}>{[...categories].sort((a,b)=>{const rank=(n:string)=>/boisson/i.test(n)?0:/biscuit/i.test(n)?1:/lait|frais/i.test(n)?2:/épicerie/i.test(n)?3:4;return rank(a.name)-rank(b.name);}).slice(0,8).map((cat,index) => <CategoryShortcut key={cat.id} label={cat.name} icon={categoryIcon(cat.name)} active={category === cat.id} onPress={() => { setCategory(category===cat.id?'':cat.id); setPage(1); }} />)}</ScrollView></>;
  const title = screen === 'favorites' ? 'Mes favoris' : screen === 'cart' ? 'Mon panier' : screen === 'orders' ? 'Mes commandes' : screen === 'account' ? 'Mon Toushir' : screen === 'offers' ? 'Offres' : screen === 'messages' ? 'Messages' : 'Catalogue';
  const header = ['offers','messages','orders','account'].includes(screen) ? <View style={[M.header,{minHeight:64,paddingBottom:0}]}><View style={[S.between,{minHeight:64}]}>{screen==='account'&&accountPanel?<Pressable accessibilityRole="button" accessibilityLabel="Retour à Mon Toushir" onPress={()=>setAccountPanel(null)} style={M.headerAction}><Icon name="arrow-back" size={22}/></Pressable>:null}<Text style={[M.screenTitle,{fontSize:screen==='account'&&accountPanel?22:28,lineHeight:32,flex:1}]}>{screen==='account'&&accountPanel?accountTitles[accountPanel]:title}</Text>{screen==='offers'?<Pressable accessibilityRole="button" accessibilityLabel="Mes favoris" style={M.headerAction} onPress={()=>navigate('favorites')}><FigmaImage source={require('../assets/figma/mobile-merchant-home-71cce.svg')} style={{width:22,height:22}}/></Pressable>:screen==='account'&&!accountPanel?<Pressable accessibilityRole="button" accessibilityLabel="Paramètres du compte" style={M.headerAction} onPress={()=>setAccountPanel('settings')}><FigmaImage source={require('../assets/figma/mobile-merchant-account-60888.svg')} style={{width:22,height:22}}/></Pressable>:screen==='messages'?<Pressable accessibilityRole="button" accessibilityLabel="Préférences de communication" onPress={()=>setCommunicationSettings(n=>!n)} style={M.headerAction}><FigmaImage source={require('../assets/figma/mobile-merchant-account-60888.svg')} style={{width:22,height:22}}/></Pressable>:screen==='orders'?<Button variant="ghost" icon="refresh-outline" label="Actualiser les commandes" onPress={()=>setRefresh(n=>n+1)}/>:null}</View></View> : <View style={M.header}>
    <View style={[S.between,{minHeight:64}]}><FigmaImage source={require('../assets/figma/mobile-merchant-home-ef546.svg')} style={{width:112,height:29}} contentFit="contain" accessibilityLabel="Toushir"/><View style={S.row}><Pressable accessibilityRole="button" accessibilityLabel="Mes favoris" onPress={()=>navigate('favorites')} style={M.headerAction}><FigmaImage source={require('../assets/figma/mobile-merchant-home-71cce.svg')} style={{width:22,height:22}}/></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Mes commandes et notifications" onPress={()=>navigate('orders')} style={M.headerAction}><FigmaImage source={require('../assets/figma/mobile-merchant-home-2e62d.svg')} style={{width:22,height:22}}/></Pressable></View></View>
    <Pressable accessibilityRole="button" accessibilityLabel="Choisir l’adresse de livraison" onPress={()=>setLocationOpen(true)} style={[S.row,{minHeight:44,gap:10}]}><FigmaImage source={require('../assets/figma/mobile-merchant-home-237ad.svg')} style={{width:18,height:18}}/><Text style={[S.label,{flex:1}]} numberOfLines={1}>{selectedAddress?.label||session.user.shopName||'Mon commerce'} · {selectedAddress?.commune||'Oran'}</Text><Icon name="chevron-down" size={14}/></Pressable>
    {(screen === 'catalog' || screen === 'favorites') ? <View style={[S.row,{gap:10}]}><View style={[M.search,{flex:1,marginTop:0}]}><FigmaImage source={require('../assets/figma/mobile-merchant-home-16ce4.svg')} style={{width:22,height:22}}/><TextInput accessibilityLabel="Rechercher un produit, une marque ou un code-barres" placeholder="Produit, marque, code-barres" placeholderTextColor={C.muted} value={search} onChangeText={v => { setSearch(v); if(screen==='favorites') navigate('catalog'); }} style={M.searchInput} returnKeyType="search"/>{search?<Pressable accessibilityRole="button" accessibilityLabel="Effacer la recherche" onPress={()=>setSearch('')} style={M.iconButton}><Icon name="close-circle" color={C.muted} size={20}/></Pressable>:null}</View><Pressable accessibilityRole="button" accessibilityLabel="Rechercher un produit par code-barres" onPress={()=>{setSearch('');onNotice('Saisissez le code-barres dans le champ de recherche.');}} style={[M.headerAction,{backgroundColor:C.ink}]}><FigmaImage source={require('../assets/figma/mobile-merchant-home-59ff6.svg')} style={{width:22,height:22}}/></Pressable></View> : <View style={[S.between,{paddingTop:14,paddingBottom:3}]}><Text style={M.screenTitle}>{title}</Text>{screen==='orders'?<Button variant="ghost" icon="refresh-outline" label="Actualiser les commandes" onPress={()=>setRefresh(n=>n+1)}/>:screen==='cart'?<Text style={S.small}>{cart.length} références · {cartCount} unités</Text>:null}</View>}
  </View>;
  const demoStrip = session.workspace.isDemo ? <View style={M.demoStrip}><View style={S.row}><Icon name="flask-outline" size={12} color={C.muted} /><Text style={M.demoText}>Mode démo</Text></View>{session.workspace.canSwitchRole?<Pressable accessibilityRole="button" onPress={handleSwitch} style={{ paddingVertical: 8, paddingLeft: 12, flexDirection: 'row', gap: 4, alignItems: 'center' }}><Text style={[M.demoText, { fontWeight: '700', color: C.ink }]}>Espace grossiste</Text><Icon name="arrow-up-right-box-outline" size={12} color={C.ink} /></Pressable>:null}</View> : null;
  const sectionHeader = (text: string, detail?: string, action?: React.ReactNode) => <View style={[S.between, { marginTop: 20, marginBottom: 16, alignItems: 'flex-start' }]}><View style={{ flex: 1, gap: 5 }}><Text style={M.sectionTitle}>{text}</Text>{detail ? <Text style={S.muted}>{detail}</Text> : null}</View>{action}</View>;
  const checkoutFooter = screen === 'cart' && cart.length > 0 && quote ? <View style={M.checkoutFooter}><View style={[S.between, { paddingBottom: 12 }]}><Text style={S.label}>{quote.needsQuote ? 'Montant après devis' : 'Total de la commande'}</Text><Text style={[S.subheading, { fontSize: 17 }]}>{quote.needsQuote ? 'À confirmer' : money(quote.total)}</Text></View><Button icon="arrow-forward" loading={busy || cartSaving} disabled={!addressId || cartSaveError || session.user.approvalStatus === 'PENDING'} onPress={checkout}>{quote.needsQuote ? 'Demander mon devis' : 'Confirmer la commande'}</Button>{cartSaveError ? <Button variant="ghost" onPress={() => saveCart([...cart])}>Réessayer l’enregistrement du panier</Button> : null}<Text style={[S.small, { textAlign: 'center', marginTop: 8 }]}>Vous validez l’offre avant toute préparation.</Text></View> : null;

  return <View style={M.app}>{header}{demoStrip}
    {error ? <View style={{ paddingHorizontal: 16, paddingTop: 10 }}><Notice error>{error}</Notice><Button small variant="ghost" icon="refresh-outline" onPress={() => { if (cartSaveError) saveCart([...cart]); else setRefresh(n => n + 1); }}>Réessayer</Button></View> : null}
    {screen === 'catalog' ? <FlatList ref={catalogRef} key="merchant-two-columns" data={loading ? [] : products} numColumns={2} keyExtractor={p => p.id} renderItem={({ item }) => productCard(item)} columnWrapperStyle={{ gap: 12, marginBottom: 12 }} contentContainerStyle={{ paddingHorizontal: 20, paddingBottom: 24 }} refreshControl={<RefreshControl refreshing={loading} onRefresh={() => setRefresh(n => n + 1)} tintColor={C.green} />} ListHeaderComponent={<>
      {!query && !category && page===1 && incomingOrder ? <Pressable accessibilityRole="button" accessibilityLabel="Suivre ma livraison" onPress={()=>api<Order>(`/orders/${incomingOrder.id}`).then(setOrder).catch(e=>setError(e.message))} style={[M.valueStrip,{backgroundColor:C.pale,padding:14}]}><FigmaImage source={require('../assets/figma/mobile-merchant-home-8ac27.svg')} style={{width:24,height:24}}/><View style={{flex:1,gap:3}}><Text style={M.valueTitle}>{incomingOrder.status==='OUT_FOR_DELIVERY'?'Votre commande arrive':incomingOrder.status==='READY'?'Votre commande est prête':'Votre commande se prépare'}</Text><Text style={[M.valueText,{color:C.green}]}>{incomingOrder.number} · consulter le suivi</Text></View></Pressable> : null}
      {categoryStrip}<View style={[S.between, { paddingBottom: 15, paddingTop: 4 }]}><View style={{ flex: 1, paddingRight: 10 }}><Text style={M.sectionTitle}>{query ? `« ${query} »` : category ? categories.find(c => c.id === category)?.name || category : 'Pour votre réassort'}</Text><Text style={[S.small, { marginTop: 4 }]}>{total.toLocaleString('fr-DZ')} produits{category ? '' : ' au catalogue'}</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Trier les produits" onPress={() => setSortOpen(true)} style={M.sort}><Icon name="swap-vertical-outline" size={17} color={C.ink} /><Text style={S.label}>Trier</Text></Pressable></View>{loading ? <Loading count={2} /> : null}
    </>} ListEmptyComponent={!loading ? <Empty title="Aucun produit trouvé" message="Essayez un autre nom, une marque ou une catégorie." icon="search-outline" action={<Button variant="secondary" onPress={() => { setSearch(''); setCategory(''); }}>Tout le catalogue</Button>} /> : undefined} ListFooterComponent={totalPages > 1 ? <View style={[S.row, { justifyContent: 'space-between', marginTop: 14 }]}><Button variant="secondary" icon="chevron-back" disabled={page <= 1} onPress={() => setPage(p => p - 1)} label="Page précédente" /><Text style={S.small}>Page {page} sur {totalPages}</Text><Button variant="secondary" icon="chevron-forward" disabled={page >= totalPages} onPress={() => setPage(p => p + 1)} label="Page suivante" /></View> : undefined} /> : <ScrollView ref={contentRef} keyboardShouldPersistTaps="handled" contentContainerStyle={{ paddingHorizontal: 20, paddingBottom: 24 }}>
      {screen==='offers'?<Campaigns session={session} onNotice={onNotice} onProduct={id=>api<Product>(`/products/${id}`).then(setSelectedProduct).catch(e=>setError(e.message))}/>:null}
      {screen==='messages'?<Conversations session={session} settingsOpen={communicationSettings}/>:null}
      {screen === 'favorites' ? <>{sectionHeader('Les essentiels de votre commerce', 'Retrouvez vos produits pour le prochain réassort.')}<View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>{favoriteProducts.map(p => <React.Fragment key={p.id}>{productCard(p)}</React.Fragment>)}</View>{!favoriteProducts.length ? <Empty title="Gardez vos essentiels" message="Touchez le cœur sur un produit pour le retrouver ici." icon="heart-outline" action={<Button onPress={() => navigate('catalog')}>Explorer le catalogue</Button>} /> : null}</> : null}
      {screen === 'cart' ? <>{cart.length && quote ? <View style={{ gap: 16, paddingTop: 16 }}>
        <View style={M.group}>{quote.items.filter(item => cart.some(line => line.productId === item.product.id)).map((item, i, values) => <View key={item.product.id} style={[M.cartRow, { borderBottomWidth: i < values.length - 1 ? 1 : 0 }]}><ProductImage uri={item.product.imageUrl} name={item.product.name} size={70} /><View style={{ flex: 1, minWidth: 0, gap: 6 }}><Text style={M.brand}>{item.product.brand}</Text><Text style={[S.text, { fontWeight: '600', lineHeight: 19 }]} numberOfLines={2}>{item.product.name}</Text><Text style={S.small}>{quantityLabel(item.product)}</Text><View style={[S.between, { flexWrap: 'wrap', marginTop: 5 }]}><Text style={S.label}>{money(item.unitPrice)}</Text><QuantityControl compact value={cart.find(l => l.productId === item.product.id)?.quantity || 0} minimum={item.product.minimumQuantity || 1} onChange={n => changeQuantity(item.product, n)} name={item.product.name} /></View></View></View>)}</View>
        <Button variant="secondary" icon="add" onPress={() => navigate('catalog')}>Ajouter des produits</Button>
        <View style={M.group}><View style={[S.between, { marginBottom: 12 }]}><Text style={M.sectionTitle}>Adresse de livraison</Text><Button variant="ghost" small onPress={() => setLocationOpen(true)}>Modifier</Button></View>{selectedAddress ? <View style={S.row}><Icon name="location-outline" color={C.green} /><View style={{ flex: 1 }}><Text style={S.label}>{selectedAddress.label}</Text><Text style={S.muted}>{selectedAddress.street}</Text><Text style={S.small}>{selectedAddress.commune}, {selectedAddress.wilaya}</Text></View></View> : <Button variant="secondary" icon="add" onPress={() => setAddressForm({ label: 'Mon commerce', commune: 'Oran', street: '' })}>Ajouter une adresse</Button>}</View>
        <View style={M.group}><Text style={[M.sectionTitle, { marginBottom: 14 }]}>Votre récapitulatif</Text><Info label="Produits" value={money(quote.subtotal)} /><View style={{ height: 12 }} /><Info label="Livraison" value={quote.needsQuote || quote.deliveryFee == null ? 'À confirmer' : money(quote.deliveryFee)} />{quote.needsQuote ? <Text style={[S.small, { marginTop: 14 }]}>Le grossiste vous propose un prix et des quantités disponibles. Rien n’est réservé avant votre acceptation.</Text> : null}</View>
        <View style={M.group}><Field label="Une précision pour le grossiste ?" value={note} onChangeText={setNote} placeholder="Accès au commerce, préférence de livraison…" multiline /></View>
      </View> : <Empty title="Votre panier est vide" message="Choisissez les produits dont votre commerce a besoin. Votre grossiste confirmera son offre." icon="bag-handle-outline" action={<Button onPress={() => navigate('catalog')}>Voir les produits</Button>} />}</> : null}
      {screen === 'orders' ? <><ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8, paddingVertical: 17 }}>{[['', 'Toutes'], ['REQUESTED', 'Demandes'], ['QUOTED', 'Offres à valider'], ['OUT_FOR_DELIVERY', 'En livraison'], ['DELIVERED', 'Livrées']].map(([value, label]) => <CategoryPill key={value} label={label} active={orderFilter === value} onPress={() => { setOrderFilter(value); setOrderPage(1); }} />)}</ScrollView>{loading ? <Loading count={2} /> : orders.length ? <View style={{ gap: 12 }}>{orders.map(o => <Pressable accessibilityRole="button" accessibilityLabel={`Ouvrir ${o.number}`} onPress={async () => { try { setOrder(await api<Order>(`/orders/${o.id}`)); } catch (e: any) { setError(e.message); } }} key={o.id} style={({ pressed }) => [M.orderCard, { opacity: pressed ? .7 : 1 }]}><View style={S.between}><Status value={o.status} /><Icon name="chevron-forward" size={18} color={C.muted} /></View><View style={[S.row, { marginVertical: 16 }]}><ProductImage uri={o.imageUrl} name={`Aperçu de ${o.number}`} size={58} /><View style={{ flex: 1, gap: 4 }}><Text style={S.label}>{o.number}</Text><Text style={S.small}>{date(o.createdAt)} · {o.itemCount} références</Text></View><Text style={[S.text, { fontWeight: '700' }]}>{money(o.total)}</Text></View>{o.status === 'QUOTED' ? <View style={M.orderPrompt}><Icon name="document-text-outline" size={17} color={C.green} /><Text style={[S.label, { color: C.green, flex: 1 }]}>Votre offre est prête à valider</Text></View> : <Text style={S.small}>{o.status === 'REQUESTED' ? 'Votre grossiste prépare son offre.' : statuses[o.status]}</Text>}</Pressable>)}</View> : <Empty title="Vos commandes, au même endroit" message="Après votre première demande, vous pourrez suivre chaque étape ici." icon="receipt-outline" action={<Button onPress={() => navigate('catalog')}>Commencer une demande</Button>} />}{orderPages > 1 ? <View style={[S.between, { marginTop: 20 }]}><Button variant="secondary" icon="chevron-back" label="Commandes précédentes" disabled={orderPage === 1} onPress={() => setOrderPage(p => p - 1)} /><Text style={S.small}>{orderPage} / {orderPages}</Text><Button variant="secondary" icon="chevron-forward" label="Commandes suivantes" disabled={orderPage === orderPages} onPress={() => setOrderPage(p => p + 1)} /></View> : null}</> : null}
      {screen === 'account' ? <MerchantAccount session={session} panel={accountPanel} onPanel={setAccountPanel} addresses={addresses} onAddAddress={()=>setAddressForm({label:'Mon commerce',commune:'Oran',street:''})} onOrders={()=>navigate('orders')} onJoin={onJoin} onSwitch={handleSwitch} onLogout={handleLogout} onNotice={onNotice}/>:null}
    </ScrollView>}
    {checkoutFooter}
    {screen!=='cart'&&cart.length>0?<View style={{paddingHorizontal:20,paddingTop:8,paddingBottom:8,backgroundColor:'white'}}><View style={[S.between,{borderWidth:1,borderColor:C.border,borderRadius:16,padding:8,paddingLeft:14,minHeight:56}]}><View style={{flex:1}}><Text style={[M.price,{marginBottom:0}]}>{quote?.needsQuote||quote?.total==null?'Sur devis':money(quote.total)}</Text><Text style={S.small}>{cartCount} unités · hors livraison</Text></View><Button style={{minWidth:152,minHeight:44}} icon="arrow-forward" onPress={()=>navigate('cart')}>Voir le panier</Button></View></View>:null}
    <View style={M.tabBar}>{nav.map(item => <Pressable accessibilityRole="tab" accessibilityState={{ selected: screen === item.id }} key={item.id} onPress={() => navigate(item.id)} style={({ pressed }) => [M.tab, { opacity: pressed ? .6 : 1 }]}><View><FigmaImage source={item.id==='catalog'?require('../assets/figma/mobile-merchant-home-41ec3.svg'):item.id==='offers'?require('../assets/figma/mobile-merchant-home-dca2a.svg'):item.id==='orders'?require('../assets/figma/mobile-merchant-home-83fb6.svg'):item.id==='messages'?require('../assets/figma/mobile-merchant-home-2e2be.svg'):require('../assets/figma/mobile-merchant-home-c5de3.svg')} style={{width:24,height:24}} tintColor={screen===item.id?C.green:C.muted}/>{item.id === 'cart' && cart.length > 0 ? <View style={M.tabBadge}><Text style={{ color: C.white, fontSize: 9, fontWeight: '700' }}>{cart.length}</Text></View> : null}</View><Text style={[M.tabText, { color: screen === item.id ? C.green : C.muted, fontWeight: screen === item.id ? '700' : '500' }]}>{item.label}</Text></Pressable>)}</View>

    <Modal visible={!!selectedProduct} animationType="slide" transparent onRequestClose={() => setSelectedProduct(null)}><View style={M.overlay}>{selectedProduct ? <View style={[M.sheet, { maxHeight: '93%' }]}><View style={M.sheetHandle} /><View style={[S.between, { paddingHorizontal: 20 }]}><Text style={[S.label, { color: C.muted }]}>{selectedProduct.brand}</Text><Button label="Fermer la fiche produit" icon="close" variant="ghost" onPress={() => setSelectedProduct(null)} /></View><ScrollView contentContainerStyle={{ paddingHorizontal: 20, paddingBottom: Math.max(insets.bottom, 20), gap: 15 }}><ProductImage uri={selectedProduct.imageUrl} name={selectedProduct.name} style={{ height: 235, aspectRatio: undefined }} /><Text style={M.screenTitle}>{selectedProduct.name}</Text><Text style={S.muted}>{quantityLabel(selectedProduct)}</Text><Text style={[M.price, { fontSize: 22 }]}>{money(selectedProduct.price)}</Text>{!selectedProduct.offerApproved ? <Notice>Le prix, le colisage et la disponibilité seront confirmés dans l’offre du grossiste.</Notice> : null}<View style={S.divider} /><Info label="Code-barres" value={selectedProduct.barcode} /><Info label="Catégorie" value={selectedProduct.category} /><View style={{ gap: 12 }}>{cartQuantity(selectedProduct) ? <QuantityControl value={cartQuantity(selectedProduct)} minimum={selectedProduct.minimumQuantity || 1} onChange={n => changeQuantity(selectedProduct, n)} name={selectedProduct.name} /> : <Button icon="add" onPress={() => changeQuantity(selectedProduct, selectedProduct.minimumQuantity || 1)}>Ajouter au panier</Button>}<Button variant="secondary" onPress={() => { setSelectedProduct(null); navigate('cart'); }}>Voir mon panier</Button></View>{selectedProduct.sourceUrl ? <Button variant="ghost" icon="open-outline" onPress={() => Linking.openURL(selectedProduct.sourceUrl!).catch(e => setError(e.message))}>Voir la fiche source</Button> : null}<Text style={S.small}>Données Open Food Facts · ODbL. Photos des contributeurs · CC BY-SA 3.0. Signalez une différence d’emballage avant de confirmer le devis.</Text></ScrollView></View> : null}</View></Modal>
    <Modal visible={!!order} animationType="slide" onRequestClose={() => setOrder(null)}>{order ? <SafeAreaView style={{ flex: 1, backgroundColor: C.white }}><View style={M.orderAppBar}><Button icon="arrow-back" label="Retour aux commandes" variant="ghost" onPress={() => setOrder(null)} /><Text style={M.sectionTitle}>Ma commande</Text><Button variant="ghost" icon="refresh-outline" label="Actualiser le détail de la commande" onPress={() => api<Order>(`/orders/${order.id}`).then(setOrder).catch(e => setError(e.message))} /></View><ScrollView contentContainerStyle={{ width: '100%', maxWidth: 520, alignSelf: 'center', padding: 16, gap: 16, paddingBottom: 30 }}>
      {error ? <Notice error>{error}</Notice> : null}<View style={M.group}><View style={S.between}><Text style={S.label}>{order.number}</Text><Status value={order.status} /></View><Text style={[S.small, { marginTop: 7 }]}>{date(order.createdAt, true)}</Text><OrderProgress status={order.status} /></View>
      {order.status === 'QUOTED' ? <View style={M.group}><Text style={M.sectionTitle}>Votre offre est prête</Text><Text style={[S.muted, { marginTop: 8 }]}>Vérifiez les quantités et le prix proposé. Votre accord réserve le stock.</Text><Text style={[S.small, { marginVertical: 13 }]}>Version {order.quoteVersion} · valable jusqu’au {date(order.quoteValidUntil, true)}</Text><Button loading={busy} onPress={() => orderAction('accept-quote', { version: order.quoteVersion }, 'Offre acceptée. Votre commande est confirmée.')} icon="checkmark">Accepter · {money(order.total)}</Button><Button variant="ghost" disabled={busy} onPress={() => orderAction('decline', { reason: 'Offre déclinée par le commerçant.' }, 'Offre déclinée.')}>Décliner cette offre</Button></View> : order.status === 'REQUESTED' ? <Notice>Demande bien reçue. Le devis de votre grossiste apparaîtra ici dès qu’il sera prêt.</Notice> : null}
      {['PREPARING','READY','OUT_FOR_DELIVERY','DELIVERED','PARTIALLY_DELIVERED','DELIVERY_REFUSED'].includes(order.status) ? <MerchantDelivery order={order} onRefresh={() => api<Order>(`/orders/${order.id}`).then(setOrder).catch(e => setError(e.message))} /> : null}
      <View style={M.group}><Text style={[M.sectionTitle, { marginBottom: 8 }]}>Mes produits</Text>{order.items.map(item => <View key={item.id || item.productId} style={M.orderLine}><ProductImage uri={item.imageUrl} name={item.name} size={58} /><View style={{ flex: 1, gap: 4 }}><Text style={[S.text, { fontWeight: '600' }]}>{item.name}</Text><Text style={S.small}>{item.quantity} unités{item.caseSize && item.caseSize > 1 ? ` · colis de ${item.caseSize}` : ''}{item.requestedQuantity !== item.quantity ? ` · ${item.requestedQuantity} demandées` : ''}</Text>{['DELIVERED', 'PARTIALLY_DELIVERED', 'DELIVERY_REFUSED'].includes(order.status) ? <Text style={S.small}>{item.acceptedQuantity || 0} acceptées · {item.refusedQuantity || 0} refusées</Text> : null}<Text style={S.label}>{money(item.lineTotal)}</Text></View></View>)}<View style={[S.between, { paddingTop: 14 }]}><Text style={S.muted}>Livraison</Text><Text style={S.label}>{order.deliveryFee == null ? 'À confirmer' : money(order.deliveryFee)}</Text></View><View style={[S.between, { marginTop: 16, paddingTop: 15, borderTopWidth: 1, borderColor: C.border }]}><Text style={M.sectionTitle}>{order.total == null ? 'Total à confirmer' : 'Total confirmé'}</Text><Text style={M.sectionTitle}>{money(order.total)}</Text></View></View>
      <OrderGifts orderId={order.id}/>
      <View style={M.group}><View style={[S.between, { marginBottom: 17 }]}><Text style={M.sectionTitle}>Paiement</Text><Status value={order.paymentStatus} /></View><Info label="Encaissé" value={money(order.balance.collected)} /><View style={{ height: 12 }} /><Info label="Reste dû" value={order.total == null ? 'À confirmer' : money(order.balance.remaining)} />{order.balance.refundDue > 0 ? <View style={{ marginTop: 12 }}><Info label="À rembourser" value={money(order.balance.refundDue)} /></View> : null}<Text style={[S.small, { marginTop: 14 }]}>Une livraison confirmée ne signifie pas que le paiement est enregistré.</Text></View>
      <View style={M.group}><Text style={[M.sectionTitle, { marginBottom: 12 }]}>Livraison</Text><View style={S.row}><Icon name="location-outline" color={C.green} /><View style={{ flex: 1 }}><Text style={S.label}>{order.address?.label}</Text><Text style={S.muted}>{order.address?.street}, {order.address?.commune}</Text></View></View>{order.note ? <Text style={[S.muted, { marginTop: 14 }]}>Votre note : {order.note}</Text> : null}{order.adminNote ? <Text style={[S.muted, { marginTop: 12 }]}>Message du grossiste : {order.adminNote}</Text> : null}</View>
      <MerchantClaims order={order} onUpdated={setOrder} onNotice={onNotice} />
      <View style={M.group}><Text style={[M.sectionTitle, { marginBottom: 18 }]}>Historique</Text>{order.timeline.map((event, index) => <View key={event.id || index} style={{ flexDirection: 'row', gap: 13 }}><View style={{ alignItems: 'center', width: 12 }}><View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: index === order.timeline.length - 1 ? C.green : '#ADB5B0', marginTop: 5 }} />{index < order.timeline.length - 1 ? <View style={{ width: 1, flex: 1, minHeight: 30, backgroundColor: C.border, marginTop: 5 }} /> : null}</View><View style={{ flex: 1, paddingBottom: 20, gap: 4 }}><Text style={S.label}>{event.details || statuses[event.action] || event.action.replaceAll('_', ' ')}</Text><Text style={S.small}>{event.actor} · {date(event.at, true)}</Text></View></View>)}</View>
      <Button variant="secondary" icon="refresh-outline" loading={busy} onPress={() => orderAction('reorder', {}, 'Produits remis au panier avec les conditions actuelles.')}>Commander à nouveau</Button><Button variant="ghost" icon="document-text-outline" onPress={() => { const result = Platform.OS === 'web' ? Linking.openURL(`${API_ORIGIN}/api/orders/${order.id}/document`) : Share.share({ message: `${order.number}\n${statuses[order.status]}\n${order.items.map(i => `${i.quantity} × ${i.name}`).join('\n')}\n${money(order.total)}` }); result.catch(e => setError(e.message)); }}>Bon de commande</Button>{['REQUESTED', 'QUOTED', 'SUBMITTED', 'CONFIRMED'].includes(order.status) ? <Button variant="danger" disabled={busy} onPress={confirmCancel}>Annuler la demande</Button> : null}
    </ScrollView></SafeAreaView> : null}</Modal>
    <Modal visible={locationOpen} transparent animationType="slide" onRequestClose={() => setLocationOpen(false)}><View style={M.overlay}><View style={[M.sheet, { padding: 20, paddingBottom: Math.max(insets.bottom, 20), gap: 16 }]}><View style={S.between}><Text style={M.sectionTitle}>Livrer à cette adresse</Text><Button icon="close" label="Fermer les adresses" variant="ghost" onPress={() => setLocationOpen(false)} /></View>{addresses.map(a => <Pressable key={a.id} accessibilityRole="radio" accessibilityState={{ selected: a.id === addressId }} onPress={() => { setAddressId(a.id); setLocationOpen(false); }} style={M.addressChoice}><Icon name={a.id === addressId ? 'radio-button-on' : 'radio-button-off'} color={C.green} /><View style={{ flex: 1, gap: 3 }}><Text style={S.label}>{a.label}</Text><Text style={S.muted}>{a.street}</Text><Text style={S.small}>{a.commune}, {a.wilaya}</Text></View></Pressable>)}<Button variant="secondary" icon="add" onPress={() => { setLocationOpen(false); setAddressForm({ label: 'Mon commerce', commune: 'Oran', street: '' }); }}>Ajouter une adresse</Button></View></View></Modal>
    <Modal visible={sortOpen} transparent animationType="slide" onRequestClose={() => setSortOpen(false)}><View style={M.overlay}><View style={[M.sheet, { padding: 20, paddingBottom: Math.max(insets.bottom, 20), gap: 6 }]}><View style={S.between}><Text style={M.sectionTitle}>Trier les produits</Text><Button icon="close" label="Fermer le tri" variant="ghost" onPress={() => setSortOpen(false)} /></View>{[['featured', 'Sélection Toushir'], ['name', 'Nom du produit'], ['newest', 'Dernières références'], ['price_asc', 'Prix croissant'], ['price_desc', 'Prix décroissant']].map(([value, label]) => <Pressable key={value} accessibilityRole="radio" accessibilityState={{ selected: sort === value }} onPress={() => { setSort(value); setPage(1); setSortOpen(false); }} style={M.addressChoice}><Text style={[S.text, { flex: 1 }]}>{label}</Text><Icon name={sort === value ? 'radio-button-on' : 'radio-button-off'} color={sort === value ? C.green : C.muted} /></Pressable>)}<Text style={[S.small, { marginTop: 10 }]}>Les produits sur devis restent séparés des prix confirmés.</Text></View></View></Modal>
    <Modal visible={!!addressForm} transparent animationType="slide" onRequestClose={() => setAddressForm(null)}><KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={M.overlay}><View style={M.sheet}><ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ padding: 20, paddingBottom: Math.max(insets.bottom, 20), gap: 17 }}><View style={S.between}><Text style={M.sectionTitle}>Nouvelle adresse</Text><Button label="Fermer l’adresse" icon="close" variant="ghost" onPress={() => setAddressForm(null)} /></View>{error ? <Notice error>{error}</Notice> : null}<Field label="Nom de l’adresse" value={addressForm?.label || ''} onChangeText={v => setAddressForm(f => f && ({ ...f, label: v }))} /><Field label="Commune d’Oran" value={addressForm?.commune || ''} onChangeText={v => setAddressForm(f => f && ({ ...f, commune: v }))} /><Field label="Rue et numéro" value={addressForm?.street || ''} onChangeText={v => setAddressForm(f => f && ({ ...f, street: v }))} /><Button loading={busy} onPress={saveAddress}>Enregistrer l’adresse</Button></ScrollView></View></KeyboardAvoidingView></Modal>
  </View>;
}

function CategoryShortcut({ label, icon, active, onPress }: { label: string; icon: string; active: boolean; onPress: () => void }) { return <Pressable accessibilityRole="button" accessibilityState={{ selected: active }} onPress={onPress} style={({ pressed }) => [M.category, { opacity: pressed ? .6 : 1 }]}><View style={[M.categoryIcon, { backgroundColor: active ? C.green : /boisson|eau|jus/i.test(label) ? C.mint : /lait|fromage|laitier|frais/i.test(label) ? '#CCFFFF' : C.bg }]}><FigmaImage source={/water/.test(icon)?require('../assets/figma/mobile-merchant-home-55d78.svg'):/ice-cream/.test(icon)?require('../assets/figma/mobile-merchant-home-91e4a.svg'):/cafe/.test(icon)?require('../assets/figma/mobile-merchant-home-8b747.svg'):require('../assets/figma/mobile-merchant-home-4dda0.svg')} style={{width:28,height:28}} /></View><Text style={[M.categoryLabel, { color: active ? C.green : C.muted, fontWeight: active ? '700' : '500' }]} numberOfLines={2}>{label}</Text></Pressable>; }
function CategoryPill({label,active,onPress}:{label:string;active:boolean;onPress:()=>void}) {return <FilterPill selected={active} onPress={onPress}>{label}</FilterPill>;}
function QuantityControl({ value, onChange, minimum, name, compact = false }: { value: number; onChange: (n: number) => void; minimum: number; name: string; compact?: boolean }) { return <View style={[M.quantity, compact ? { width: 132 } : {}]}><Pressable accessibilityRole="button" accessibilityLabel={`Diminuer ${name}`} onPress={() => onChange(value <= minimum ? 0 : Math.max(minimum, value - minimum))} style={M.quantityButton}><Icon name={value <= minimum ? 'trash-outline' : 'remove'} color={C.green} size={18} /></Pressable><Text accessibilityLabel={`Quantité ${value}`} style={[S.label, { flex: 1, textAlign: 'center', color: C.green, fontSize: 14 }]}>{value}</Text><Pressable accessibilityRole="button" accessibilityLabel={`Augmenter ${name}`} onPress={() => onChange(value + minimum)} style={M.quantityButton}><Icon name="add" color={C.green} size={19} /></Pressable></View>; }
function Info({ label, value }: { label: string; value: string }) { return <View style={[S.between, { alignItems: 'flex-start' }]}><Text style={[S.muted, { flexShrink: 0 }]}>{label}</Text><Text style={[S.text, { textAlign: 'right', flexShrink: 1 }]}>{value}</Text></View>; }
function OrderProgress({ status }: { status: string }) { const active = status === 'REQUESTED' ? 0 : status === 'QUOTED' ? 1 : ['SUBMITTED', 'CONFIRMED', 'PREPARING', 'READY'].includes(status) ? 2 : ['OUT_FOR_DELIVERY', 'DELIVERED', 'PARTIALLY_DELIVERED', 'DELIVERY_REFUSED'].includes(status) ? 3 : -1; if (active < 0) return null; return <View accessibilityLabel={`Suivi : ${statuses[status]}`} style={{ flexDirection: 'row', marginTop: 24 }}>{['Demande', 'Offre', 'Préparation', 'Livraison'].map((label, i) => <View key={label} style={{ flex: 1, gap: 8 }}><View style={{ flexDirection: 'row', alignItems: 'center' }}><View style={{ width: 9, height: 9, borderRadius: 5, backgroundColor: i <= active ? C.green : C.border }} />{i < 3 ? <View style={{ height: 2, flex: 1, backgroundColor: i < active ? C.green : C.border }} /> : null}</View><Text style={[S.small, { color: i <= active ? C.ink : C.muted, fontSize: 10, fontWeight: i === active ? '700' : '500' }]}>{label}</Text></View>)}</View>; }

const M = StyleSheet.create({
  app: { flex: 1, width: '100%', maxWidth: 520, alignSelf: 'center', backgroundColor: '#FFFFFF', borderLeftWidth: Platform.OS === 'web' ? 1 : 0, borderRightWidth: Platform.OS === 'web' ? 1 : 0, borderColor: '#E7EAE8' },
  header: { backgroundColor: '#FFFFFF', paddingHorizontal: 20, paddingTop: 0, paddingBottom: 16, borderBottomWidth: 1, borderColor: '#E7EBE7' },
  headerAction: {width:44,height:44,borderRadius:24,alignItems:'center',justifyContent:'center',backgroundColor:'#F4F6F4'},
  location: { flex: 1, minWidth: 0, flexDirection: 'row', justifyContent: 'flex-end', alignItems: 'center', gap: 6, minHeight: 44, paddingLeft: 10 },
  locationSmall: { fontFamily: 'Inter', fontSize: 11, lineHeight: 15, color: '#6B736F' },
  locationName: { fontFamily: 'Inter', fontSize: 12, fontWeight: '600', lineHeight: 18, color: '#151917' },
  search: { flexDirection: 'row', alignItems: 'center', gap: 10, backgroundColor: '#F3F4F4', borderRadius: 11, paddingLeft: 13, marginTop: 11 },
  searchInput: { fontFamily: 'Inter', color: '#151917', flex: 1, minWidth: 0, height: 48, fontSize: 13, paddingVertical: 12 },
  iconButton: { width: 43, height: 44, alignItems: 'center', justifyContent: 'center' },
  demoStrip: { backgroundColor: '#FFFFFF', borderBottomWidth: 1, borderColor: '#E7EBE7', paddingHorizontal: 16, minHeight: 33, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  demoText: { fontFamily: 'Inter', color: '#6B736F', fontSize: 11, lineHeight: 16 },
  screenTitle: { fontFamily: 'Inter', fontSize: 23, fontWeight: '700', lineHeight: 30, letterSpacing: -.45, color: '#151917' },
  sectionTitle: { fontFamily: 'Inter', fontSize: 17, lineHeight: 23, fontWeight: '700', letterSpacing: -.25, color: '#151917' },
  valueStrip: { marginTop: 15, borderRadius: 10, backgroundColor: '#FFFFFF', paddingVertical: 12, paddingHorizontal: 13, flexDirection: 'row', alignItems: 'center', gap: 11 },
  valueTitle: { fontFamily: 'Inter', color: '#151917', fontSize: 13, lineHeight: 18, fontWeight: '600' },
  valueText: { fontFamily: 'Inter', color: '#6B736F', fontSize: 11, lineHeight: 16 },
  categoryRail: { paddingTop: 0, paddingBottom:16, gap: 12 }, category: { width: 81, alignItems: 'center', gap: 8 },
  categoryIcon: { width: 76, height: 60, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFFFFF' },
  categoryLabel: { fontFamily: 'Inter', fontSize: 11, lineHeight: 14, textAlign: 'center', minHeight: 28 },
  sort: { flexDirection: 'row', alignItems: 'center', gap: 6, minHeight: 44, paddingLeft: 12, paddingRight: 2 },
  productCard: { backgroundColor: '#FFFFFF', overflow: 'hidden' },
  packshot: { backgroundColor: '#FFFFFF', paddingTop: 5, position: 'relative' },
  favorite: { position: 'absolute', top: 3, right: 2, width: 44, height: 44, alignItems: 'center', justifyContent: 'center', backgroundColor: '#FFFFFFDD', borderRadius: 22 },
  productBody: { padding: 0, paddingTop: 4, gap: 4, flex: 1 }, brand: { fontFamily: 'Inter', fontSize: 11, lineHeight: 16, color: '#6B736F', fontWeight: '500' },
  productName: { fontFamily: 'Inter', fontSize: 14, lineHeight: 20, fontWeight: '600', color: '#151917', minHeight: 34 },
  pack: { fontFamily: 'Inter', fontSize: 11, lineHeight: 16, color: '#6B736F', marginBottom: 5 },
  price: { fontFamily: 'Inter', fontSize: 18, lineHeight: 25, fontWeight: '700', color: '#151917', marginTop: 'auto', marginBottom: 5 },
  addButton: { minHeight:44,width:44,borderRadius:22,backgroundColor:C.ink,alignItems:'center',justifyContent:'center' },
  addText: { fontFamily: 'Inter', color: '#087F4F', fontSize: 12, fontWeight: '700', lineHeight: 18 },
  quantity: { height: 44, backgroundColor: '#F4F8F5', borderRadius: 8, borderWidth: 1, borderColor: '#D2E3D8', flexDirection: 'row', alignItems: 'center' },
  quantityButton: { minWidth: 42, height: 43, alignItems: 'center', justifyContent: 'center' },
  group: { backgroundColor: '#FFFFFF', borderRadius: 12, padding: 18, borderWidth: 1, borderColor: '#E7EBE7' },
  cartRow: { flexDirection: 'row', alignItems: 'flex-start', gap: 13, paddingVertical: 15, borderColor: '#E7EBE7' },
  checkoutFooter: { padding: 16, borderTopWidth: 1, borderColor: '#E7EBE7', backgroundColor: '#FFFFFF' },
  basketBar: { marginHorizontal: 16, marginBottom: 10, marginTop: 7, backgroundColor: '#087F4F', minHeight: 49, borderRadius: 10, paddingHorizontal: 13, flexDirection: 'row', alignItems: 'center', gap: 10 },
  basketCount: { minWidth: 25, minHeight: 25, borderRadius: 6, backgroundColor: '#FFFFFF22', alignItems: 'center', justifyContent: 'center', paddingHorizontal: 4 },
  tabBar: { backgroundColor: '#FFFFFF', flexDirection: 'row', borderTopWidth: 1, borderColor: '#E7EBE7', paddingTop: 7, paddingBottom: 7 },
  tab: { flex: 1, minHeight: 54, justifyContent: 'center', alignItems: 'center', gap: 5 },
  tabText: { fontFamily: 'Inter', fontSize: 11, lineHeight: 14 },
  tabBadge: { position: 'absolute', right: -8, top: -5, minWidth: 16, height: 16, borderRadius: 8, backgroundColor: '#087F4F', alignItems: 'center', justifyContent: 'center', paddingHorizontal: 3, borderWidth: 1, borderColor: '#FFFFFF' },
  orderCard: { backgroundColor: '#FFFFFF', borderWidth: 1, borderColor: '#E7EBE7', borderRadius: 12, padding: 16 },
  orderPrompt: { flexDirection: 'row', alignItems: 'center', gap: 8, paddingTop: 12, borderTopWidth: 1, borderColor: '#E7EBE7' },
  orderAppBar: { minHeight: 60, paddingHorizontal: 12, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', borderBottomWidth: 1, borderColor: '#E7EBE7', width: '100%', maxWidth: 520, alignSelf: 'center' },
  orderLine: { flexDirection: 'row', alignItems: 'flex-start', gap: 13, paddingVertical: 14, borderBottomWidth: 1, borderColor: '#E7EBE7' },
  shopAvatar: { width: 55, height: 55, borderRadius: 15, backgroundColor: '#F2F5F3', alignItems: 'center', justifyContent: 'center' },
  overlay: { flex: 1, backgroundColor: '#00000066', justifyContent: 'flex-end', alignItems: 'center' },
  sheet: { backgroundColor: '#FFFFFF', width: '100%', maxWidth: 520, borderTopLeftRadius: 20, borderTopRightRadius: 20, maxHeight: '94%', overflow: 'hidden' },
  sheetHandle: { width: 33, height: 4, borderRadius: 3, backgroundColor: '#D9DFDB', alignSelf: 'center', marginTop: 9, marginBottom: 8 },
  addressChoice: { flexDirection: 'row', gap: 12, alignItems: 'center', paddingVertical: 16, borderBottomWidth: 1, borderColor: '#E7EBE7', minHeight: 50 },
});
