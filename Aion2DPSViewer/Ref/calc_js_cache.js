const WING_EFFECTS_DATA = {
  '공허의 탈리스라 날개': { attackPower: 60, cooldownReduction: 4, additionalAccuracy: 35 },
  '봄 꽃 나비의 날개': { attackPower: 60, additionalAccuracy: 35 },
  '악몽의 날개': { attackPower: 60, damageAmplification: 3.5, criticalHit: 35, additionalAccuracy: 35 },
  '어둠의 장막 날개': { attackPower: 60, damageAmplification: 2.5 },
  '숲 정령의 날개': { bossAttackPower: 95, damageAmplification: 3.5 }, // 보스 공격력은 퍼센트 적용 후 마지막에 더함
  '크로메데의 날개': { attackPower: 60, criticalHit: 35 },
  '검은 파편의 날개': { criticalAttackPower: 95, criticalHit: 35 }, // 치명타 공격력은 특수 처리
  '고대 아울라우의 날개': { attackPower: 60 },
  '정복자의 날개': { bossAttackPower: 80 }, // 보스 공격력은 퍼센트 적용 후 마지막에 더함
  '보라꽃나비 날개': { attackPower: 40 }, 
  '드라마타 둥지의 날개' : { bossAttackPower: 95, pveAccuracy: 45, damageAmplification: 3.5 },
  '무아의 날개': { stunHit: 3 } // 강타 +3%
};

function round(value, decimals) {
  return Number(Math.round(value + 'e' + decimals) + 'e-' + decimals);
}

function getWingEffects() {
  const wingName = window.currentWingName || '';
  if (!wingName) return null;
  
  // 날개 이름에서 효과 데이터 찾기 (부분 일치도 지원)
  for (const [name, effects] of Object.entries(WING_EFFECTS_DATA)) {
    if (wingName.includes(name) || name.includes(wingName)) {
      return { name, ...effects };
    }
  }
  return null;
}

function getWingEffectsByName(wingName) {
  if (!wingName) return null;
  
  // 날개 이름에서 효과 데이터 찾기 (부분 일치도 지원)
  for (const [name, effects] of Object.entries(WING_EFFECTS_DATA)) {
    if (wingName.includes(name) || name.includes(wingName)) {
      return { name, ...effects };
    }
  }
  return null;
}

function applyBlackShardWingEffect() {
  const wingEffects = getWingEffects();
  if (!wingEffects || !wingEffects.criticalAttackPower) {
    return; // 검은 파편의 날개가 아니면 처리하지 않음
  }
  
  // 치명타 결과에서 치명타 정수 가져오기
  if (!window.criticalHitResult || !window.criticalHitResult.breakdown) {
    return;
  }
  
  // displayCriticalHitStats와 동일한 계산 로직 사용
  // 최종 치명타 정수 계산: (정수 합계 + 날개 + 타이틀 장착 효과) * (1 + (주신 스탯 퍼센트 + 일반 스탯 퍼센트)/100)
  const breakdown = window.criticalHitResult.breakdown;
  const { baseCriticalHitInteger, soulCriticalHitInteger, stoneCriticalHitInteger, daevanionCriticalHitInteger, wingCriticalHitInteger, titleEquipCriticalHit, deathCriticalHitPercent, accuracyCriticalHitPercent } = breakdown;
  
  const totalCriticalHitInteger = (baseCriticalHitInteger || 0) + (soulCriticalHitInteger || 0) + (stoneCriticalHitInteger || 0) + (daevanionCriticalHitInteger || 0) + (wingCriticalHitInteger || 0) + (titleEquipCriticalHit || 0);
  const totalPercentMultiplier = 1 + (((deathCriticalHitPercent || 0) + (accuracyCriticalHitPercent || 0)) / 100);
  const finalCriticalHitInteger = Math.round(totalCriticalHitInteger * totalPercentMultiplier);
  
  // 실제 치명타 확률 계산: (스탯 × 0.4) / 10 = 확률%, 80% 캡 적용
  const criticalChance = Math.min((finalCriticalHitInteger * 0.4) / 10, 80); // 예: 1019 * 0.4 / 10 = 40.76%, 최대 80%
  const criticalAttackPowerBonus = Math.round(wingEffects.criticalAttackPower * (criticalChance / 100));
  
  // 공격력 결과가 없으면 처리하지 않음
  if (!window.attackPowerResult) {
    return;
  }
  
  // 기존 공격력에 치명타 공격력 보너스 추가
  const oldFinalAttack = window.attackPowerResult.finalAttack;
  
  // 검은 파편의 날개 효과는 퍼센트 적용 전에 더해지므로, 퍼센트 배율도 적용해야 함
  const attackBreakdown = window.attackPowerResult.breakdown;
  const percentMultiplier = (attackBreakdown.transcendPercent || 0) + (attackBreakdown.destructionPercent || 0) + (attackBreakdown.powerPercent || 0);
  const bonusWithPercent = Math.floor(criticalAttackPowerBonus * (1 + percentMultiplier / 100));
  
  // 최종 공격력 업데이트
  window.attackPowerResult.finalAttack = oldFinalAttack + bonusWithPercent;
  window.attackPowerResult.breakdown.wingAttackPower = (window.attackPowerResult.breakdown.wingAttackPower || 0) + criticalAttackPowerBonus;
  window.attackPowerResult.breakdown.wingCriticalAttackPower = criticalAttackPowerBonus;
  window.attackPowerResult.breakdown.wingCriticalChance = criticalChance;
  
  // 공격력 표시 업데이트
  const attackPowerValueElement = document.getElementById('attack-power-value');
  if (attackPowerValueElement) {
    attackPowerValueElement.textContent = window.attackPowerResult.finalAttack.toLocaleString();
  }
  
  // 날개 효과 tooltip 업데이트
  const tooltipWingAttack = document.getElementById('tooltip-wing-attack');
  if (tooltipWingAttack) {
    const wingValue = window.attackPowerResult.breakdown.wingAttackPower || 0;
    tooltipWingAttack.textContent = `+${wingValue.toLocaleString()}`;
  }
}

function convertCritStatToChance(critStat) {
  // 스탯 × 0.4 / 10 = 확률%
  // 예: 924 × 0.4 = 369.6 / 10 = 36.96%
  const chance = (critStat * 0.4) / 10;
  return chance;
}

function calculateAccuracyDamageIncrease(accuracyValue) {
  // 명중 1200 이하: 0% (50% 패리)
  if (accuracyValue <= 1200) {
    return 0;
  }
  
  // 명중 1700 이상: 14.2% (100% 명중, 50% 헤드/50% 백 가정)
  if (accuracyValue >= 1700) {
    return 14.2;
  }
  
  // 구간별 딜증 증가량 (50당)
  const intervals = [
    { start: 1200, end: 1250, increase: 1.6 },
    { start: 1250, end: 1300, increase: 1.6 },
    { start: 1300, end: 1350, increase: 1.5 },
    { start: 1350, end: 1400, increase: 1.5 },
    { start: 1400, end: 1450, increase: 1.4 },
    { start: 1450, end: 1500, increase: 1.4 },
    { start: 1500, end: 1550, increase: 1.4 },
    { start: 1550, end: 1600, increase: 1.3 },
    { start: 1600, end: 1650, increase: 1.3 },
    { start: 1650, end: 1700, increase: 1.2 }
  ];
  
  let totalDamageIncrease = 0;
  
  for (const interval of intervals) {
    if (accuracyValue <= interval.start) {
      // 현재 명중이 구간 시작보다 작으면 종료
      break;
    }
    
    if (accuracyValue >= interval.end) {
      // 구간을 완전히 통과한 경우, 전체 딜증 추가
      totalDamageIncrease += interval.increase;
    } else {
      // 구간 내에 있는 경우, linear interpolation
      const progress = (accuracyValue - interval.start) / (interval.end - interval.start);
      totalDamageIncrease += interval.increase * progress;
      break;
    }
  }
  
  return totalDamageIncrease;
}

function calculateAttackPower(equipment, accessories, statData, daevanionData) {
  // 중복 계산 방지: 이미 계산 중이면 스킵
  if (window.isCalculatingAttackPower) {
    return null;
  }
  
  window.isCalculatingAttackPower = true;
  
  let totalIntegerAttack = 0; // 정수 절대값 증가
  let totalPercentAttack = 0; // 퍼센트 증가
  
  // Breakdown 정보 추적
  let daevanionAttackTotal = 0; // 데바니온으로 인한 공격력 증가 (4개 보드)
  let daevanionArielAttackTotal = 0; // 데바니온 아리엘 보드로 인한 공격력 증가 (PVE/보스 공격력)
  let equipmentAttackTotal = 0; // 장비/장신구로 인한 공격력 증가 (초월 포함 전체)
  let equipmentAttackBase = 0; // 장비/장신구 기본 공격력 (초월 없이)
  let equipmentTranscendAttack = 0; // 장비/장신구 초월로 인한 공격력 증가 (정수 증가분 + 퍼센트 증가분)
  let totalTranscendInteger = 0; // 모든 장비/장신구 초월 정수 증가분 합산
  let totalBaseAttackForTranscend = 0; // 초월 계산용: (기본 옵션 공격력 + 초월 정수) 합산
  let totalTranscendPercent = 0; // 모든 장비/장신구 초월 퍼센트 합산
  let destructionPercent = 0; // 주신 스탯 파괴로 인한 퍼센트 증가
  let powerPercent = 0; // 위력으로 인한 퍼센트 증가 (일반 스탯 + 영혼 각인 합산)
  let normalStatPowerPercent = 0; // 일반 스탯 위력으로 인한 퍼센트 증가
  let soulPowerPercent = 0; // 영혼 각인 위력으로 인한 퍼센트 증가
  let soulAttackIncreasePercent = 0; // 영혼 각인 "공격력 증가" 옵션으로 인한 퍼센트 증가
  
  const logs = [];
  
  // 1. 메인 무기/가더의 기본 옵션 (정수, 강화에 따른 수치 포함)
  // 무기/가더 판별: slotPos 사용 (1=메인 무기, 2=가더) 또는 첫번째 장비가 무기
  const weaponAndGauntlet = [...(equipment || [])].filter((item, idx) => {
    // slotPos, slot_pos, slot_index, slot, raw_data.slotPos 등 다양한 경로 확인
    let slotPos = -1;
    
    if (item.slotPos !== undefined && item.slotPos !== null) slotPos = item.slotPos;
    else if (item.slot_pos !== undefined && item.slot_pos !== null) slotPos = item.slot_pos;
    else if (item.slot_index !== undefined && item.slot_index !== null) slotPos = item.slot_index;
    else if (item.slot !== undefined && item.slot !== null) slotPos = item.slot;
    else if (item.raw_data && item.raw_data.slotPos !== undefined && item.raw_data.slotPos !== null) slotPos = item.raw_data.slotPos;
    
    // slotPos가 1이면 메인 무기, 2이면 가더
    // 문자열일 수도 있으므로 == 비교 사용하거나 변환 후 비교
    const isSlotMatch = slotPos == 1 || slotPos == 2;
    
    // 또는 첫번째 장비는 무기로 간주 (장비 리스트에서 무기가 첫번째로 오는 경우)
    // 또는 이름에 무기 키워드가 포함된 경우
    const weaponKeywords = ['검', '도끼', '활', '창', '지팡이', '마도서', '권갑', '단검', '대검', '석궁', '건', '오브', '메이스', '할버드'];
    const itemName = (item.name || '').toLowerCase();
    const isWeaponByName = idx === 0 || weaponKeywords.some(keyword => itemName.includes(keyword));
    
    return isSlotMatch || (idx === 0 && isWeaponByName);
  });
  
  weaponAndGauntlet.forEach((item, index) => {
    const itemName = item.name || '알 수 없음';
    const enhanceLevel = parseInt(item.enhance_level || item.enchantLevel || 0);
    const exceedLevel = parseInt(item.exceed_level || item.exceedLevel || 0);
    
    // 무기/가더 판별: slotPos 사용 (1=메인 무기, 2=가더) 또는 첫번째 장비
    let slotPos = -1;
    if (item.slotPos !== undefined && item.slotPos !== null) slotPos = item.slotPos;
    else if (item.slot_pos !== undefined && item.slot_pos !== null) slotPos = item.slot_pos;
    else if (item.slot_index !== undefined && item.slot_index !== null) slotPos = item.slot_index;
    else if (item.slot !== undefined && item.slot !== null) slotPos = item.slot;
    else if (item.raw_data && item.raw_data.slotPos !== undefined) slotPos = item.raw_data.slotPos;
    
    // 첫번째 장비(index 0)는 무기로 간주
    const isWeapon = slotPos == 1 || slotPos === '1' || index === 0;
    const isGauntlet = slotPos == 2 || slotPos === '2';
    
    // mainStats에서 공격력 추출
    let baseAttack = 0;
    let enhanceBonus = 0;
    let minAttack = 0; // 최소 공격력
    let maxAttack = 0; // 최대 공격력
    
    // mainStats 검색
    if (item.main_stats) {
      // 배열인 경우 (createEquipmentItem과 동일한 방식)
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach((stat, statIndex) => {
          if (!stat || typeof stat !== 'object') return;
          
          const statName = stat.name || stat.id || '';
          const statValue = stat.value || '';
          const statMinValue = stat.minValue || '';
          const statExtra = stat.extra || '0';
          
          // 공격력 찾기
          if (statName.includes('공격력') || statName.toLowerCase().includes('attack')) {
            // minValue에서 최소 공격력 추출
            if (statMinValue) {
              if (typeof statMinValue === 'string') {
                const minMatch = statMinValue.match(/(\d+)\s*\(\+(\d+)\)/);
                if (minMatch) {
                  minAttack += parseInt(minMatch[1]) || 0;
                  minAttack += parseInt(minMatch[2]) || 0;
                } else {
                  const minNum = parseInt(statMinValue.replace(/[^\d]/g, '')) || 0;
                  if (minNum > 0) {
                    minAttack += minNum;
                  }
                }
              } else if (typeof statMinValue === 'number') {
                minAttack += statMinValue;
              }
            }
            
            // value에서 최대 공격력 추출
            if (typeof statValue === 'string') {
              // "489 (+225)" 형식 처리
              const match = statValue.match(/(\d+)\s*\(\+(\d+)\)/);
              if (match) {
                const baseVal = parseInt(match[1]) || 0;
                const extraVal = parseInt(match[2]) || 0;
                baseAttack += baseVal;
                enhanceBonus += extraVal;
                // maxAttack은 base + extra 합계
                maxAttack += baseVal + extraVal;
              } else {
                // 숫자만 있는 경우
                const num = parseInt(statValue.replace(/[^\d]/g, '')) || 0;
                if (num > 0) {
                  baseAttack += num;
                  maxAttack += num;
                }
              }
            } else if (typeof statValue === 'number') {
              baseAttack += statValue;
              maxAttack += statValue;
            }
            
            // extra에서 강화 보너스 추출
            if (statExtra && statExtra !== '0' && statExtra !== 0 && statExtra !== '0%') {
              const extraNum = parseInt(statExtra.toString().replace(/[^\d]/g, '')) || 0;
              if (extraNum > 0) {
                enhanceBonus += extraNum;
              }
            }
          }
        });
      }
      // 객체인 경우
      else if (typeof item.main_stats === 'object' && !Array.isArray(item.main_stats)) {
        // 공격력 관련 키 찾기
        const attackKeys = ['attack', '공격력', 'attackPower', 'atk'];
        for (const key of Object.keys(item.main_stats)) {
          const keyLower = key.toLowerCase();
          if (attackKeys.some(ak => keyLower.includes(ak))) {
            const value = item.main_stats[key];
            if (typeof value === 'string') {
              // "535 (+225)" 형태 파싱
              const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
              if (match) {
                baseAttack += parseInt(match[1]) || 0;
                enhanceBonus += parseInt(match[2]) || 0;
              } else {
                // 숫자만 있는 경우
                const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                baseAttack += num;
              }
            } else if (typeof value === 'number') {
              baseAttack += value;
            }
          }
        }
      }
    }
    
    // options에서도 공격력 찾기 (더 포괄적으로)
    if (item.options) {
      // 배열인 경우
      if (Array.isArray(item.options)) {
        item.options.forEach(option => {
          if (typeof option === 'object' && option !== null) {
            for (const key in option) {
              const keyLower = String(key).toLowerCase();
              const value = option[key];
              
              if (keyLower.includes('공격력') || keyLower.includes('attack')) {
                if (typeof value === 'string') {
                  const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
                  if (match) {
                    baseAttack += parseInt(match[1]) || 0;
                    enhanceBonus += parseInt(match[2]) || 0;
                  } else {
                    const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                    baseAttack += num;
                  }
                } else if (typeof value === 'number') {
                  baseAttack += value;
                }
              }
            }
          }
        });
      }
      // 객체인 경우
      else if (typeof item.options === 'object' && !Array.isArray(item.options)) {
        for (const key of Object.keys(item.options)) {
          const keyLower = key.toLowerCase();
          if (keyLower.includes('공격력') || keyLower.includes('attack')) {
            const value = item.options[key];
            if (typeof value === 'string') {
              const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
              if (match) {
                baseAttack += parseInt(match[1]) || 0;
                enhanceBonus += parseInt(match[2]) || 0;
              } else {
                const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                baseAttack += num;
              }
            } else if (typeof value === 'number') {
              baseAttack += value;
            }
          }
        }
      }
    }
    
    // raw_data에서도 재귀적으로 찾기
    if (item.raw_data) {
      const findAttackInData = (data, path = '') => {
        if (typeof data === 'string') {
          const match = data.match(/(\d+)\s*\(\+(\d+)\)/);
          if (match && (data.includes('공격력') || data.includes('attack'))) {
            const base = parseInt(match[1]) || 0;
            const enhance = parseInt(match[2]) || 0;
            if (base > 0 || enhance > 0) {
              baseAttack += base;
              enhanceBonus += enhance;
            }
          }
        } else if (Array.isArray(data)) {
          data.forEach((item, idx) => {
            findAttackInData(item, `${path}[${idx}]`);
          });
        } else if (typeof data === 'object' && data !== null) {
          for (const key in data) {
            const keyLower = String(key).toLowerCase();
            if (keyLower.includes('공격력') || keyLower.includes('attack') || 
                keyLower.includes('main') || keyLower.includes('option')) {
              findAttackInData(data[key], path ? `${path}.${key}` : key);
            }
          }
        }
      };
      findAttackInData(item.raw_data);
    }
    
    // 초월 정수 보너스 계산 (무기/가더: 초월 +1당 +30)
    let exceedIntegerBonus = 0;
    if (exceedLevel > 0) {
      exceedIntegerBonus = exceedLevel * 30;
    }
    
    // 초월 퍼센트 (초월 +1 당 공격력 +1%)
    const exceedPercent = exceedLevel;
    
    // 무기(slotPos 1)의 최소/최대 공격력 저장 (완벽 계산용)
    if (isWeapon) {
      // maxAttack이 0이면 baseAttack + enhanceBonus 사용
      if (maxAttack === 0 && baseAttack > 0) {
        maxAttack = baseAttack + enhanceBonus;
      }
      
      // minAttack이 0이면 maxAttack의 85% 사용
      if (minAttack === 0 && maxAttack > 0) {
        minAttack = Math.floor(maxAttack * 0.85);
      }
      
      if (maxAttack > 0) {
        // 초월 보너스 적용 안 함 - 화면에 표시된 원래 값 그대로 저장
        window.weaponMinAttack = minAttack;
        window.weaponMaxAttack = maxAttack;
      }
    }
    
    // 기본 옵션 공격력 계산: baseAttack + enhanceBonus (초월 없이)
    // 메인 무기(isWeapon)의 경우: min~max 공격력의 평균값 사용
    // 그 외 장비: max 값 그대로 사용
    if (baseAttack > 0 || enhanceBonus > 0) {
      let baseAttackOnly;
      
      if (isWeapon && minAttack > 0 && maxAttack > 0 && minAttack < maxAttack) {
        // 메인 무기: min과 max의 평균값 사용 (강화 보너스는 이미 maxAttack에 포함됨)
        // minAttack과 maxAttack은 이미 강화 보너스가 포함된 값임
        // 예: 327~654 (+350) → minAttack=327, maxAttack=1004 (654+350)
        // 하지만 현재 코드에서 minAttack은 강화 보너스 없이 저장되므로 조정 필요
        // enhanceBonus가 있으면 minAttack에도 더해야 함
        const adjustedMinAttack = minAttack + enhanceBonus;
        const adjustedMaxAttack = baseAttack + enhanceBonus; // maxAttack 대신 baseAttack + enhanceBonus
        baseAttackOnly = Math.round((adjustedMinAttack + adjustedMaxAttack) / 2);
        logs.push(`[${itemName}] 기본 옵션 공격력: (${minAttack}+${enhanceBonus} + ${baseAttack}+${enhanceBonus}) / 2 = ${baseAttackOnly} (min~max 평균 사용)`);
      } else {
        // 그 외 장비: 기존 방식 (max 값)
        baseAttackOnly = baseAttack + enhanceBonus;
      }
      
      totalIntegerAttack += baseAttackOnly;
      equipmentAttackBase += baseAttackOnly; // Breakdown 정보 저장 (기본)
      
      if (exceedLevel > 0) {
        // 초월 정수 증가분 합산
        totalTranscendInteger += exceedIntegerBonus;
        // 초월 계산용: (기본 옵션 공격력 + 초월 정수) 합산
        totalBaseAttackForTranscend += baseAttackOnly + exceedIntegerBonus;
        totalTranscendPercent += exceedPercent;
        if (!isWeapon || minAttack === 0) {
          logs.push(`[${itemName}] 기본 옵션 공격력: ${baseAttack} + 강화 ${enhanceBonus} = ${baseAttackOnly} (초월: ${exceedPercent}%, 초월정수: ${exceedIntegerBonus}) (최소: ${minAttack}, 최대: ${maxAttack})`);
        } else {
          logs.push(`[${itemName}] 기본 옵션 공격력 (평균): ${baseAttackOnly} (초월: ${exceedPercent}%, 초월정수: ${exceedIntegerBonus}) (최소: ${minAttack}, 최대: ${maxAttack})`);
        }
      } else {
        if (!isWeapon || minAttack === 0) {
          logs.push(`[${itemName}] 기본 옵션 공격력: ${baseAttack} + 강화 ${enhanceBonus} = ${baseAttackOnly} (최소: ${minAttack}, 최대: ${maxAttack})`);
        }
      }
    } else if (exceedIntegerBonus > 0) {
      // baseAttack이 0이지만 초월 정수가 있는 경우
      totalTranscendInteger += exceedIntegerBonus;
      totalBaseAttackForTranscend += exceedIntegerBonus;
      totalTranscendPercent += exceedPercent;
    }
  });
  
  // 1-2. 장신구의 기본 옵션 공격력 추출
  const accessoriesList = [...(accessories || [])];
  accessoriesList.forEach(item => {
    const itemName = item.name || '알 수 없음';
    
    
    let baseAttack = 0;
    let enhanceBonus = 0;
    
    // mainStats 검색 (무기/가더와 동일한 로직)
    if (item.main_stats) {
      // 배열인 경우
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach(stat => {
          if (typeof stat === 'object' && stat !== null) {
            // name 필드가 '공격력'인 경우
            const statName = String(stat.name || '').toLowerCase();
            if (statName.includes('공격력') || statName.includes('attack')) {
              // value 필드를 우선 사용 (실제 기본 공격력), 없으면 minValue 사용
              const baseValue = parseInt(stat.value || stat.minValue || 0);
              // extra 필드에서 강화 보너스 추출
              const extraValue = parseInt(stat.extra || 0);
              
              if (baseValue > 0 || extraValue > 0) {
                baseAttack += baseValue;
                enhanceBonus += extraValue;
              }
            }
            
            // 기존 방식: 모든 필드에서 공격력 찾기 (백업)
            for (const key in stat) {
              const keyLower = String(key).toLowerCase();
              const value = stat[key];
              
              // 이미 처리한 필드는 스킵
              if (key === 'name' || key === 'minValue' || key === 'value' || key === 'extra') {
                continue;
              }
              
              if (keyLower.includes('공격력') || keyLower.includes('attack')) {
                if (typeof value === 'string') {
                  const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
                  if (match) {
                    baseAttack += parseInt(match[1]) || 0;
                    enhanceBonus += parseInt(match[2]) || 0;
                  } else {
                    const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                    baseAttack += num;
                  }
                } else if (typeof value === 'number') {
                  baseAttack += value;
                }
              }
            }
          }
        });
      }
      // 객체인 경우
      else if (typeof item.main_stats === 'object' && !Array.isArray(item.main_stats)) {
        // 공격력 관련 키 찾기
        const attackKeys = ['attack', '공격력', 'attackPower', 'atk'];
        for (const key of Object.keys(item.main_stats)) {
          const keyLower = key.toLowerCase();
          if (attackKeys.some(ak => keyLower.includes(ak))) {
            const value = item.main_stats[key];
            if (typeof value === 'string') {
              // "99 (+100)" 형태 파싱
              const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
              if (match) {
                baseAttack += parseInt(match[1]) || 0;
                enhanceBonus += parseInt(match[2]) || 0;
              } else {
                // 숫자만 있는 경우
                const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                baseAttack += num;
              }
            } else if (typeof value === 'number') {
              baseAttack += value;
            }
          }
        }
      }
    }
    
    // options에서도 공격력 찾기
    if (item.options) {
      // 배열인 경우
      if (Array.isArray(item.options)) {
        item.options.forEach(option => {
          if (typeof option === 'object' && option !== null) {
            for (const key in option) {
              const keyLower = String(key).toLowerCase();
              const value = option[key];
              
              if (keyLower.includes('공격력') || keyLower.includes('attack')) {
                if (typeof value === 'string') {
                  const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
                  if (match) {
                    baseAttack += parseInt(match[1]) || 0;
                    enhanceBonus += parseInt(match[2]) || 0;
                  } else {
                    const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                    baseAttack += num;
                  }
                } else if (typeof value === 'number') {
                  baseAttack += value;
                }
              }
            }
          }
        });
      }
      // 객체인 경우
      else if (typeof item.options === 'object' && !Array.isArray(item.options)) {
        for (const key of Object.keys(item.options)) {
          const keyLower = key.toLowerCase();
          if (keyLower.includes('공격력') || keyLower.includes('attack')) {
            const value = item.options[key];
            if (typeof value === 'string') {
              const match = value.match(/(\d+)\s*\(\+(\d+)\)/);
              if (match) {
                baseAttack += parseInt(match[1]) || 0;
                enhanceBonus += parseInt(match[2]) || 0;
              } else {
                const num = parseInt(value.replace(/[^\d]/g, '')) || 0;
                baseAttack += num;
              }
            } else if (typeof value === 'number') {
              baseAttack += value;
            }
          }
        }
      }
    }
    
    // raw_data에서도 재귀적으로 찾기
    if (item.raw_data) {
      const findAttackInData = (data, path = '') => {
        if (typeof data === 'string') {
          const match = data.match(/(\d+)\s*\(\+(\d+)\)/);
          if (match && (data.includes('공격력') || data.includes('attack'))) {
            const base = parseInt(match[1]) || 0;
            const enhance = parseInt(match[2]) || 0;
            if (base > 0 || enhance > 0) {
              baseAttack += base;
              enhanceBonus += enhance;
            }
          }
        } else if (Array.isArray(data)) {
          data.forEach((item, idx) => {
            findAttackInData(item, `${path}[${idx}]`);
          });
        } else if (typeof data === 'object' && data !== null) {
          for (const key in data) {
            const keyLower = String(key).toLowerCase();
            if (keyLower.includes('공격력') || keyLower.includes('attack') || 
                keyLower.includes('main') || keyLower.includes('option')) {
              findAttackInData(data[key], path ? `${path}.${key}` : key);
            }
          }
        }
      };
      findAttackInData(item.raw_data);
    }
    
    // 초월 정수 보너스 계산 (장신구: 초월 +1당 +20)
    const exceedLevel = parseInt(item.exceed_level || 0);
    let exceedIntegerBonus = 0;
    if (exceedLevel > 0) {
      exceedIntegerBonus = exceedLevel * 20;
    }
    
    // 초월 퍼센트 (초월 +1 당 공격력 +1%)
    const exceedPercent = exceedLevel;
    
    // 기본 옵션 공격력 계산: baseAttack + enhanceBonus (초월 없이)
    if (baseAttack > 0 || enhanceBonus > 0) {
      const baseAttackOnly = baseAttack + enhanceBonus;
      
      totalIntegerAttack += baseAttackOnly;
      equipmentAttackBase += baseAttackOnly; // Breakdown 정보 저장 (기본)
      
      if (exceedLevel > 0) {
        // 초월 정수 증가분 합산
        totalTranscendInteger += exceedIntegerBonus;
        // 초월 계산용: (기본 옵션 공격력 + 초월 정수) 합산
        totalBaseAttackForTranscend += baseAttack + exceedIntegerBonus;
        totalTranscendPercent += exceedPercent;
        logs.push(`[${itemName}] 기본 옵션 공격력 (장신구): ${baseAttack} + 강화 ${enhanceBonus} = ${baseAttackOnly} (초월: ${exceedPercent}%, 초월정수: ${exceedIntegerBonus})`);
      } else {
        logs.push(`[${itemName}] 기본 옵션 공격력 (장신구): ${baseAttack} + 강화 ${enhanceBonus} = ${baseAttackOnly}`);
      }
    } else if (exceedIntegerBonus > 0) {
      // baseAttack이 0이지만 초월 정수가 있는 경우
      totalTranscendInteger += exceedIntegerBonus;
      totalBaseAttackForTranscend += exceedIntegerBonus;
      totalTranscendPercent += exceedPercent;
    }
  });
  
  // 2. 초월 수치에 따른 증가량은 이미 기본 옵션에 포함되었으므로 제거
  // (초월 정수 보너스와 퍼센트는 각 아이템의 기본 옵션 계산 시 적용됨)
  
  // 3. 영혼 각인 및 마석 각인에서 정수 공격력 추출 + 무기/가더 영혼 각인에서 위력 추출
  let weaponPower = 0; // 무기 영혼 각인 위력
  let gauntletPower = 0; // 가더 영혼 각인 위력
  
  const allItems = [...(equipment || []), ...(accessories || [])];
  allItems.forEach(item => {
    const itemName = item.name || '알 수 없음';
    const itemNameLower = itemName.toLowerCase();
    const categoryName = (item.category_name || '').toLowerCase();
    const itemType = (item.type || '').toLowerCase();
    
    // slotPos 확인 - 다양한 경로 시도
    let slotPos = -1;
    if (item.slotPos !== undefined && item.slotPos !== null) slotPos = item.slotPos;
    else if (item.slot_pos !== undefined && item.slot_pos !== null) slotPos = item.slot_pos;
    else if (item.slot_index !== undefined && item.slot_index !== null) slotPos = item.slot_index;
    else if (item.slot !== undefined && item.slot !== null) slotPos = item.slot;
    
    // 무기/가더 판별: slotPos 사용 (1=메인 무기, 2=가더)
    const isWeapon = slotPos === 1 || slotPos === '1';
    const isGauntlet = slotPos === 2 || slotPos === '2';
    
    // subStats (영혼 각인)에서 공격력 및 위력 추출
    if (item.sub_stats) {
      let soulAttack = 0;
      let soulPower = 0;
      
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach(stat => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            // 공격력 추출
            if ((name.includes('공격력') || name.includes('attack')) && value > 0) {
              soulAttack += value;
            }
            
            // 위력 추출
            if (name.includes('위력') || name.includes('power') || name.includes('might')) {
              if (value > 0) {
                if (isWeapon) {
                  weaponPower += value;
                  soulPower += value;
                } else if (isGauntlet) {
                  gauntletPower += value;
                  soulPower += value;
                }
              }
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseInt(item.sub_stats[key] || 0);
          
          // 공격력 추출
          if ((keyLower.includes('공격력') || keyLower.includes('attack')) && value > 0) {
            soulAttack += value;
          }
          
          // 위력 추출
          if (keyLower.includes('위력') || keyLower.includes('power') || keyLower.includes('might')) {
            if (value > 0) {
              if (isWeapon) {
                weaponPower += value;
                soulPower += value;
              } else if (isGauntlet) {
                gauntletPower += value;
                soulPower += value;
              }
            }
          }
        }
      }
      
      if (soulAttack > 0) {
        totalIntegerAttack += soulAttack;
        equipmentAttackTotal += soulAttack; // Breakdown 정보 저장 (전체)
        equipmentAttackBase += soulAttack; // Breakdown 정보 저장 (기본 - 초월과 무관)
        logs.push(`[${itemName}] 영혼 각인 공격력: +${soulAttack}`);
      }
      
      if (soulPower > 0) {
        if (isWeapon) {
          logs.push(`[${itemName}] 영혼 각인 위력 (무기): +${soulPower}`);
        } else if (isGauntlet) {
          logs.push(`[${itemName}] 영혼 각인 위력 (가더): +${soulPower}`);
        } else {
          logs.push(`[${itemName}] 영혼 각인 위력: +${soulPower}`);
        }
      }
    }
    
    // magic_stone_stat (마석 각인)에서 공격력 추출
    if (item.magic_stone_stat) {
      let stoneAttack = 0;
      if (Array.isArray(item.magic_stone_stat)) {
        item.magic_stone_stat.forEach(stat => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            if ((name.includes('공격력') || name.includes('attack')) && value > 0) {
              stoneAttack += value;
            }
          }
        });
      } else if (typeof item.magic_stone_stat === 'object') {
        for (const key of Object.keys(item.magic_stone_stat)) {
          const keyLower = key.toLowerCase();
          if (keyLower.includes('공격력') || keyLower.includes('attack')) {
            const value = parseInt(item.magic_stone_stat[key] || 0);
            if (value > 0) {
              stoneAttack += value;
            }
          }
        }
      }
      
      if (stoneAttack > 0) {
        totalIntegerAttack += stoneAttack;
        equipmentAttackTotal += stoneAttack; // Breakdown 정보 저장 (전체)
        equipmentAttackBase += stoneAttack; // Breakdown 정보 저장 (기본 - 초월과 무관)
        logs.push(`[${itemName}] 마석 각인 공격력: +${stoneAttack}`);
      }
    }
  });
  
  // 4. 데바니온 (아리엘, 아스펠 제외) 5개 보드에서의 추가 공격력
  const daevanionBoardIds = [41, 42, 43, 44, 47]; // 네자칸, 지켈, 바이젤, 트리니엘, 마르쿠탄
  const daevanionBoardNames = { 41: '네자칸', 42: '지켈', 43: '바이젤', 44: '트리니엘', 47: '마르쿠탄' };
  let daevanionAttack = 0;
  let daevanionMarkutanAttackValue = 0; // 마르쿠탄 추가 공격력 (별도 추적)
  const daevanionAttackLogs = [];
  
  if (daevanionData) {
    // 디버그: daevanionData 키 확인
    daevanionBoardIds.forEach(boardId => {
      const boardName = daevanionBoardNames[boardId] || `보드${boardId}`;
      const boardData = daevanionData[boardId];
      let boardAttack = 0;
      
      if (boardData && boardData.nodeList) {
        // open 값이 1, "1", true 등일 수 있으므로 유연하게 처리
        const activeNodes = boardData.nodeList.filter(n => n.open == 1 || n.open === true || n.open === 'true');
        
        activeNodes.forEach((node, nodeIndex) => {
          let nodeAttack = 0;
          let foundInField = null;
          
          // 노드의 모든 필드를 순회하며 검색
          for (const key in node) {
            const value = node[key];
            
            // 문자열인 경우 패턴 검색
            if (typeof value === 'string' && value.trim()) {
              const text = value;
              
              // 다양한 패턴 시도
              // 1. "추가 공격력 +5" 패턴
              let matches = text.match(/(?:추가\s*)?공격력\s*[+＋]\s*(\d+)/gi);
              if (!matches) {
                // 2. "공격력 +5" 패턴 (더 넓은 패턴)
                matches = text.match(/공격력\s*[+＋]\s*(\d+)/gi);
              }
              if (!matches) {
                // 3. "추가 공격력+5" (공백 없음)
                matches = text.match(/추가\s*공격력\s*[+＋](\d+)/gi);
              }
              if (!matches) {
                // 4. "공격력+5" (공백 없음)
                matches = text.match(/공격력\s*[+＋](\d+)/gi);
              }
              
              if (matches) {
                matches.forEach(match => {
                  const numMatch = match.match(/(\d+)/);
                  if (numMatch) {
                    const attackValue = parseInt(numMatch[1]) || 0;
                    if (attackValue > 0) {
                      nodeAttack += attackValue;
                      if (!foundInField) foundInField = key;
                    }
                  }
                });
              }
            }
            
            // 배열인 경우 (statList, effectList 등)
            if (Array.isArray(value) && value.length > 0) {
              value.forEach((item, itemIndex) => {
                if (typeof item === 'object' && item !== null) {
                  // 객체의 모든 필드 검색
                  for (const itemKey in item) {
                    const itemValue = item[itemKey];
                    if (typeof itemValue === 'string' && itemValue.trim()) {
                      let matches = itemValue.match(/(?:추가\s*)?공격력\s*[+＋]\s*(\d+)/gi);
                      if (!matches) {
                        matches = itemValue.match(/공격력\s*[+＋]\s*(\d+)/gi);
                      }
                      if (!matches) {
                        matches = itemValue.match(/추가\s*공격력\s*[+＋](\d+)/gi);
                      }
                      if (!matches) {
                        matches = itemValue.match(/공격력\s*[+＋](\d+)/gi);
                      }
                      
                      if (matches) {
                        matches.forEach(match => {
                          const numMatch = match.match(/(\d+)/);
                          if (numMatch) {
                            const attackValue = parseInt(numMatch[1]) || 0;
                            if (attackValue > 0) {
                              nodeAttack += attackValue;
                              if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                            }
                          }
                        });
                      }
                    }
                    
                    // value나 amount 필드가 숫자인 경우
                    if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                      const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                      if (itemName.includes('공격력') || itemName.includes('attack')) {
                        nodeAttack += itemValue;
                        if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                      }
                    }
                  }
                } else if (typeof item === 'string' && item.trim()) {
                  let matches = item.match(/(?:추가\s*)?공격력\s*[+＋]\s*(\d+)/gi);
                  if (!matches) {
                    matches = item.match(/공격력\s*[+＋]\s*(\d+)/gi);
                  }
                  if (matches) {
                    matches.forEach(match => {
                      const numMatch = match.match(/(\d+)/);
                      if (numMatch) {
                        const attackValue = parseInt(numMatch[1]) || 0;
                        if (attackValue > 0) {
                          nodeAttack += attackValue;
                          if (!foundInField) foundInField = `${key}[${itemIndex}]`;
                        }
                      }
                    });
                  }
                }
              });
            }
            
            // 객체인 경우 재귀적으로 검색
            if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
              for (const subKey in value) {
                const subValue = value[subKey];
                if (typeof subValue === 'string' && subValue.trim()) {
                  let matches = subValue.match(/(?:추가\s*)?공격력\s*[+＋]\s*(\d+)/gi);
                  if (!matches) {
                    matches = subValue.match(/공격력\s*[+＋]\s*(\d+)/gi);
                  }
                  if (matches) {
                    matches.forEach(match => {
                      const numMatch = match.match(/(\d+)/);
                      if (numMatch) {
                        const attackValue = parseInt(numMatch[1]) || 0;
                        if (attackValue > 0) {
                          nodeAttack += attackValue;
                          if (!foundInField) foundInField = `${key}.${subKey}`;
                        }
                      }
                    });
                  }
                }
              }
            }
          }
          
          // NC API 오류로 숫자가 누락된 경우 처리 (활성화된 "공격력" 노드인데 숫자가 없는 경우)
          // "추가 공격력", "공격력" 노드는 기본값 +5
          if (nodeAttack === 0) {
            const nodeName = node.name || node.desc || node.effect || '';
            const nodeText = String(nodeName).trim();
            // "공격력"이 포함되어 있지만, 다른 특수 공격력(PVE 공격력, 보스 공격력 등)이 아닌 경우
            if (nodeText.includes('공격력') && 
                !nodeText.includes('PVE') && !nodeText.includes('pve') && 
                !nodeText.includes('보스') && !nodeText.includes('무기')) {
              nodeAttack = 5; // 기본값 +5
              foundInField = 'API누락-기본값적용';
            }
          }
          
          if (nodeAttack > 0) {
            boardAttack += nodeAttack;
            const nodeName = node.name || node.desc || node.effect || `노드${nodeIndex}`;
            daevanionAttackLogs.push(`  [${boardName}] ${nodeName} (${foundInField || '알 수 없음'}): +${nodeAttack}`);
          }
        });
        
        if (boardAttack > 0) {
          daevanionAttack += boardAttack;
          if (boardId === 47) {
            daevanionMarkutanAttackValue = boardAttack; // 마르쿠탄 별도 추적
          }
        }
      }
    });
  }
  
  if (daevanionAttack > 0) {
    totalIntegerAttack += daevanionAttack;
    daevanionAttackTotal = daevanionAttack; // Breakdown 정보 저장 (5개 보드)
    logs.push(`[데바니온] 5개 보드 추가 공격력: +${daevanionAttack}`);
    // 상세 로그 추가
    daevanionAttackLogs.forEach(log => logs.push(log));
  } else if (daevanionData) {
    daevanionAttackTotal = 0;
    logs.push(`[데바니온] 4개 보드 추가 공격력: +0 (추가 공격력 노드 없음)`);
  }
  
  // 4-1. 데바니온 아리엘 보드에서 PVE 공격력, 보스 공격력 추출
  let daevanionArielAttack = 0;
  const daevanionArielAttackLogs = [];
  
  if (daevanionData && daevanionData[45]) {
    const boardData = daevanionData[45];
    let boardArielAttack = 0;
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node, nodeIndex) => {
        let nodeArielAttack = 0;
        let foundInField = null;
        
        // 노드의 모든 필드를 순회하며 검색
        for (const key in node) {
          const value = node[key];
          
          // 문자열인 경우 패턴 검색 (PVE 공격력, 보스 공격력)
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            
            // "PVE 공격력 +5", "보스 공격력 +5" 패턴 검색
            let matches = text.match(/(?:PVE\s*공격력|보스\s*공격력)\s*[+＋]\s*(\d+)/gi);
            if (!matches) {
              matches = text.match(/(?:PVE공격력|보스공격력)\s*[+＋]\s*(\d+)/gi);
            }
            if (!matches) {
              matches = text.match(/(?:pve\s*attack|boss\s*attack)\s*[+＋]\s*(\d+)/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+)/);
                if (numMatch) {
                  const attackValue = parseInt(numMatch[1]) || 0;
                  if (attackValue > 0) {
                    nodeArielAttack += attackValue;
                    if (!foundInField) foundInField = key;
                  }
                }
              });
            }
          }
          
          // 배열인 경우
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item, itemIndex) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/(?:PVE\s*공격력|보스\s*공격력)\s*[+＋]\s*(\d+)/gi);
                    if (!matches) {
                      matches = itemValue.match(/(?:PVE공격력|보스공격력)\s*[+＋]\s*(\d+)/gi);
                    }
                    if (!matches) {
                      matches = itemValue.match(/(?:pve\s*attack|boss\s*attack)\s*[+＋]\s*(\d+)/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+)/);
                        if (numMatch) {
                          const attackValue = parseInt(numMatch[1]) || 0;
                          if (attackValue > 0) {
                            nodeArielAttack += attackValue;
                            if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                          }
                        }
                      });
                    }
                  }
                  
                  // value나 amount 필드가 숫자인 경우
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if (itemName.includes('pve 공격력') || itemName.includes('보스 공격력') || 
                        itemName.includes('pve attack') || itemName.includes('boss attack')) {
                      nodeArielAttack += itemValue;
                      if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                    }
                  }
                }
              }
            });
          }
          
          // 객체인 경우 재귀적으로 검색
          if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
            for (const subKey in value) {
              const subValue = value[subKey];
              if (typeof subValue === 'string' && subValue.trim()) {
                let matches = subValue.match(/(?:PVE\s*공격력|보스\s*공격력)\s*[+＋]\s*(\d+)/gi);
                if (!matches) {
                  matches = subValue.match(/(?:PVE공격력|보스공격력)\s*[+＋]\s*(\d+)/gi);
                }
                if (!matches) {
                  matches = subValue.match(/(?:pve\s*attack|boss\s*attack)\s*[+＋]\s*(\d+)/gi);
                }
                
                if (matches) {
                  matches.forEach(match => {
                    const numMatch = match.match(/(\d+)/);
                    if (numMatch) {
                      const attackValue = parseInt(numMatch[1]) || 0;
                      if (attackValue > 0) {
                        nodeArielAttack += attackValue;
                        if (!foundInField) foundInField = `${key}.${subKey}`;
                      }
                    }
                  });
                }
              }
            }
          }
        }
        
        if (nodeArielAttack > 0) {
          boardArielAttack += nodeArielAttack;
          const nodeName = node.name || node.desc || node.effect || `노드${nodeIndex}`;
          daevanionArielAttackLogs.push(`  [아리엘] ${nodeName} (${foundInField || '알 수 없음'}): +${nodeArielAttack}`);
        }
      });
      
      if (boardArielAttack > 0) {
        daevanionArielAttack = boardArielAttack;
      }
    }
  }
  
  if (daevanionArielAttack > 0) {
    totalIntegerAttack += daevanionArielAttack;
    daevanionArielAttackTotal = daevanionArielAttack; // Breakdown 정보 저장 (아리엘)
    logs.push(`[데바니온] 아리엘 보드 PVE/보스 공격력: +${daevanionArielAttack}`);
    daevanionArielAttackLogs.forEach(log => logs.push(log));
  } else if (daevanionData && daevanionData[45]) {
    daevanionArielAttackTotal = 0;
    logs.push(`[데바니온] 아리엘 보드 PVE/보스 공격력: +0 (PVE/보스 공격력 노드 없음)`);
  }
  
  // 5. 주신 스탯 중 파괴[지켈] 스탯 1당 공격력 +0.1%
  if (statData && statData.statList) {
    const destructionStat = statData.statList.find(stat => 
      stat.type === 'Destruction' || (stat.name && stat.name.includes('파괴'))
    );
    if (destructionStat) {
      const destructionValue = parseInt(destructionStat.value || 0);
      destructionPercent = destructionValue * 0.2; // Breakdown 정보 저장 (1당 0.2%, 2배 적용)
      totalPercentAttack += destructionPercent;
      logs.push(`[주신 스탯] 파괴[지켈] ${destructionValue}: +${destructionPercent.toFixed(1)}%`);
    }
  }
  
  // 6. 일반 스탯 중 위력 스탯 1당 공격력 +0.1% (영혼 각인 위력 제외 - 아이온2 12월17일 업데이트로 스탯 수치에 반영됨)
  let totalPower = 0; // 총 위력 (일반 스탯만, 영혼 각인 위력 제외)
  let normalStatPower = 0; // 일반 스탯 위력
  let soulPowerTotal = 0; // 영혼 각인 위력 합계 (표시용, 계산에는 사용 안 함)
  
  if (statData && statData.statList) {
    const powerStat = statData.statList.find(stat => stat.type === 'STR');
    if (powerStat) {
      normalStatPower = parseInt(powerStat.value || 0);
      // 위력 캡 200 적용
      const cappedPower = Math.min(normalStatPower, 200);
      totalPower = cappedPower;
      
      // 영혼 각인 위력은 표시용으로만 저장 (계산에는 사용 안 함)
      soulPowerTotal = weaponPower + gauntletPower;
      
      // 일반 스탯 위력만 계산 (영혼 각인 위력 제외, 캡 200 적용)
      normalStatPowerPercent = cappedPower * 0.1;
      soulPowerPercent = 0; // 영혼 각인 위력은 계산에서 제외
      powerPercent = cappedPower * 0.1; // 캡 적용된 위력 사용
      totalPercentAttack += powerPercent;
      
      logs.push(`[일반 스탯] 위력 ${normalStatPower}${normalStatPower > 200 ? ' (캡 200 적용)' : ''}: +${powerPercent.toFixed(1)}%`);
    } else {
      // 일반 스탯 위력이 없으면 0
      normalStatPowerPercent = 0;
      soulPowerPercent = 0;
      powerPercent = 0;
    }
  } else {
    // statData가 없으면 0
    normalStatPowerPercent = 0;
    soulPowerPercent = 0;
    powerPercent = 0;
  }
  
  // 7. 장비 영혼 각인에서 "공격력 증가" 옵션 추출 (% 증가)
  const allItemsForAttackIncrease = [...(equipment || []), ...(accessories || [])];
  allItemsForAttackIncrease.forEach((item) => {
    if (!item || !item.sub_stats) return;
    if (Array.isArray(item.sub_stats)) {
      item.sub_stats.forEach((stat) => {
        if (typeof stat === 'object') {
          const name = (stat.name || stat.type || '').trim();
          const value = parseFloat(stat.value || stat.amount || 0);
          // 정확히 "공격력 증가"만 매칭 (공격력과 구분)
          if (value > 0 && name === '공격력 증가') {
            soulAttackIncreasePercent += value;
          }
        }
      });
    } else if (typeof item.sub_stats === 'object') {
      for (const key of Object.keys(item.sub_stats)) {
        const keyTrimmed = key.trim();
        const value = parseFloat(item.sub_stats[key] || 0);
        if (value > 0 && keyTrimmed === '공격력 증가') {
          soulAttackIncreasePercent += value;
        }
      }
    }
  });
  
  if (soulAttackIncreasePercent > 0) {
    totalPercentAttack += soulAttackIncreasePercent;
    logs.push(`[영혼 각인] 공격력 증가: +${soulAttackIncreasePercent.toFixed(1)}%`);
  }
  
  // 최종 장비/장신구 공격력 (기본만, 초월은 별도 계산)
  equipmentAttackTotal = equipmentAttackBase;
  
  // 날개 장착 효과로 인한 공격력 (퍼센트 적용 전에 더함)
  let wingAttackPower = 0;
  let wingBossAttackPower = 0; // 보스 공격력은 퍼센트 적용 후 마지막에 더함
  const wingEffects = getWingEffects();
  if (wingEffects) {
    // 일반 공격력 (퍼센트 적용 대상)
    if (wingEffects.attackPower) {
      wingAttackPower = wingEffects.attackPower;
      logs.push(`[날개] ${wingEffects.name}: 공격력 +${wingAttackPower}`);
    }
    // 보스 공격력 (퍼센트 적용 제외, 마지막에 더함)
    if (wingEffects.bossAttackPower) {
      wingBossAttackPower = wingEffects.bossAttackPower;
      logs.push(`[날개] ${wingEffects.name}: 보스 공격력 +${wingBossAttackPower} (퍼센트 미적용)`);
    }
  }
  
  // 타이틀 장착 효과에서 공격력 추출 (PVE 공격력, 추가 공격력)
  let titleEquipAttackPower = 0;
  const currentTitles = window.currentTitles || [];
  if (currentTitles && Array.isArray(currentTitles)) {
    currentTitles.forEach((title) => {
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          if (typeof effect === 'string') {
            // "PVE 공격력 +22" 또는 "추가 공격력 +50" 패턴 매칭
            const attackMatch = effect.match(/(?:PVE\s*공격력|추가\s*공격력)\s*\+?(\d+)/i);
            if (attackMatch) {
              titleEquipAttackPower += parseInt(attackMatch[1]) || 0;
            }
          }
        });
      }
    });
  }
  
  // 새로운 계산식: (데바니온 + 장비장신구 + 초월 정수분 + 날개 + 타이틀) * (초월 퍼센티지 + 파괴 + 위력) + 데바니온 (아리엘 PVE) + 날개 보스 공격력
  // 베이스 공격력 (정수) - 날개 일반 공격력, 타이틀 공격력도 퍼센트 적용 대상에 포함
  const baseAttack = daevanionAttackTotal + equipmentAttackBase + totalTranscendInteger + wingAttackPower + titleEquipAttackPower;
  
  // 퍼센트 증가 (초월 퍼센트 + 파괴 + 위력 + 영혼 각인 공격력 증가)
  const percentMultiplier = totalTranscendPercent + destructionPercent + powerPercent + soulAttackIncreasePercent;
  
  // 최종 계산 (일반 날개 효과가 퍼센트 상승 적용받고, 보스 공격력은 마지막에 더함)
  let finalAttack = Math.floor(baseAttack * (1 + percentMultiplier / 100)) + daevanionArielAttackTotal + wingBossAttackPower;
  
  // 아르카나 세트 효과: 광분 2세트 PVE 공격력 +50 (곱연산 후 정수로 더함)
  const arcanaSetCountsForAtk = window.arcanaSetCounts || { frenzy: 0 };
  const arcanaFrenzyPveAttack = arcanaSetCountsForAtk.frenzy >= 2 ? 50 : 0;
  finalAttack += arcanaFrenzyPveAttack;
  
  // 로그 추가
  if (totalTranscendInteger > 0 || totalTranscendPercent > 0 || wingAttackPower > 0 || wingBossAttackPower > 0 || soulAttackIncreasePercent > 0) {
    logs.push(`[계산식] 베이스: ${baseAttack.toLocaleString()} = 데바니온(${daevanionAttackTotal}) + 장비/장신구(${equipmentAttackBase}) + 초월정수(${totalTranscendInteger}) + 날개(${wingAttackPower})`);
    logs.push(`[계산식] 퍼센트: ${percentMultiplier.toFixed(1)}% = 초월퍼센트(${totalTranscendPercent}%) + 파괴(${destructionPercent.toFixed(1)}%) + 위력(${powerPercent.toFixed(1)}%) + 영혼각인공증(${soulAttackIncreasePercent.toFixed(1)}%)`);
    logs.push(`[계산식] 최종: ${baseAttack.toLocaleString()} * (1 + ${percentMultiplier.toFixed(1)}%) + 아리엘PVE(${daevanionArielAttackTotal}) + 날개보스(${wingBossAttackPower}) + 아르카나광분2세트(${arcanaFrenzyPveAttack}) = ${finalAttack.toLocaleString()}`);
  }
  
  // 로그 출력 제거 (프로덕션 최적화)
  
  // 계산 완료 플래그 해제
  window.isCalculatingAttackPower = false;
  
  const result = {
    integerAttack: totalIntegerAttack,
    percentAttack: totalPercentAttack,
    finalAttack: finalAttack,
    logs: logs,
    breakdown: {
      daevanionAttack: daevanionAttackTotal,
      daevanionMarkutanAttack: daevanionMarkutanAttackValue,
      daevanionArielAttack: daevanionArielAttackTotal,
      equipmentAttack: equipmentAttackTotal,
      equipmentAttackBase: equipmentAttackBase,
      equipmentTranscendAttack: equipmentTranscendAttack,
      transcendInteger: totalTranscendInteger,
      transcendPercent: totalTranscendPercent,
      destructionPercent: destructionPercent,
      powerPercent: powerPercent,
      normalStatPowerPercent: normalStatPowerPercent,
      soulPowerPercent: soulPowerPercent,
      soulAttackIncreasePercent: soulAttackIncreasePercent,
      wingAttackPower: wingAttackPower,
      wingBossAttackPower: wingBossAttackPower,
      wingName: wingEffects ? wingEffects.name : null,
      titleEquipAttackPower: titleEquipAttackPower,
      arcanaFrenzyPveAttack: arcanaFrenzyPveAttack // 광분 2세트: PVE 공격력 +50
    }
  };
  
  // 전역 변수에 저장 (표시용)
  window.attackPowerResult = result;
  
  // 공격력 표시 업데이트
  displayAttackPowerStats(result);
  
  // 전투 속도 계산 및 표시
  calculateCombatSpeed(equipment, accessories, statData, daevanionData, window.currentTitles || []);
  
  // 피해 증폭 계산 및 표시
  calculateDamageAmplification(equipment, accessories, daevanionData, window.currentTitles || []);
  
  // 치명타 계산 및 표시
  calculateCriticalHit(equipment, accessories, statData, daevanionData);
  
  // 검은 파편의 날개 치명타 공격력 효과 적용 (치명타 계산 완료 후)
  applyBlackShardWingEffect();
  
  // 강타 계산 및 표시 (타이틀, 지혜[루미엘], 영혼 각인)
  calculateStunHit(equipment, accessories, statData, window.currentTitles || []);
  
  // 완벽 계산 및 표시
  calculatePerfect(equipment, accessories, statData, window.currentTitles || []);
  
  // 다단 히트 적중 계산 및 표시
  calculateMultiHit(equipment, accessories, daevanionData);
  
  // 명중 계산 및 표시 (전투 점수에 포함되지 않음, 정보 표시용)
  calculateAccuracy(equipment, accessories, statData, daevanionData);
  
  // 스킬 점수 계산 및 표시 (async)
  calculateSkillDamage(window.currentSkills || [], window.currentStigmas || []).catch(error => {
    console.error('[스킬 점수 계산] 오류:', error);
  });
  
  // 재사용 대기 시간 감소 계산 및 표시
  calculateCooldownReduction(statData, daevanionData, window.currentTitles || []);
  
  // DPS 점수 계산 및 표시 (모든 스탯 계산 완료 후)
  // 데바니온 데이터가 로드 중이면 나중에 updateAllDaevanionSkillPoints에서 호출됨
  // 데바니온 데이터가 이미 완전히 로드되었거나 로딩 중이 아닌 경우에만 여기서 호출
  // 검색 ID를 캡처하여 동시 검색 시 데이터 충돌 방지
  const capturedSearchId = window.currentSearchId;
  if (window.daevanionData && Object.keys(window.daevanionData).length >= 4) {
    // 데바니온 데이터가 이미 완전히 로드된 경우 (4개 보드 이상)
    setTimeout(() => {
      // 검색 ID가 변경되었으면 다른 캐릭터 검색이 진행 중이므로 무시
      if (capturedSearchId !== window.currentSearchId) return;
      calculateDpsScore(capturedSearchId);
    }, 200);
  } else if (!isUpdatingDaevanionPoints) {
    // 데바니온 데이터가 없고 로딩 중이 아닌 경우에만 계산
    setTimeout(() => {
      // 검색 ID가 변경되었으면 다른 캐릭터 검색이 진행 중이므로 무시
      if (capturedSearchId !== window.currentSearchId) return;
      calculateDpsScore(capturedSearchId);
    }, 200);
  }
  // 데바니온 데이터가 로딩 중이면 updateAllDaevanionSkillPoints에서 계산됨
  
  return result;
}

function calculateAttackPowerWithDaevanion(equipment, accessories, statData, daevanionData) {
  return calculateAttackPower(equipment, accessories, statData, daevanionData || null);
}

function calculateAttackPowerCap() {
  // 초월 퍼센트, 파괴, 위력 값 가져오기 (전역 변수에서 직접 - 캡 적용된 값)
  const attackPowerResult = window.attackPowerResult || {};
  const breakdown = attackPowerResult.breakdown || {};
  
  const transcendPercent = breakdown.transcendPercent || parseFloat(document.getElementById('tooltip-equipment-transcend-percent')?.textContent?.replace(/[^0-9.-]/g, '') || '0');
  const destructionPercent = breakdown.destructionPercent || parseFloat(document.getElementById('tooltip-destruction')?.textContent?.replace(/[^0-9.-]/g, '') || '0');
  // 위력 (캡 200 적용)
  const powerPercent = breakdown.normalStatPowerPercent || 0;
  // 영혼 각인 - 공격력 증가
  const soulAttackIncreasePercent = breakdown.soulAttackIncreasePercent || 0;
  
  // 패시브 스킬로 인한 공격력 % 계산
  let passiveAttackPercent = 0;
  let passiveSkillName = '';
  let passiveSkillLevel = 0;
  
  const currentJob = window.currentJobName || '';
  const currentSkills = window.currentSkills || [];
  
  // 직업별 패시브 스킬 매핑
  const jobPassiveSkills = {
  
  };
  
  // 현재 직업의 패시브 스킬 확인
  const jobPassive = jobPassiveSkills[currentJob];
  if (jobPassive) {
    // 패시브 스킬 그룹에서 해당 스킬 찾기
    const passiveSkills = currentSkills.filter(skill => {
      const group = skill.group || '';
      return group.toLowerCase().includes('passive') || group === '패시브';
    });
    
    const targetSkill = passiveSkills.find(skill => skill.name === jobPassive.skillName);
    if (targetSkill) {
      passiveSkillLevel = targetSkill.level_int || parseInt(targetSkill.level || '0', 10) || 0;
      passiveAttackPercent = passiveSkillLevel * jobPassive.percentPerLevel;
      passiveSkillName = jobPassive.skillName;
    }
  }
  
  // 총 공격력 % 캡 계산
  const totalCapPercent = transcendPercent + destructionPercent + powerPercent + soulAttackIncreasePercent + passiveAttackPercent;
  
  return {
    totalCapPercent,
    transcendPercent,
    destructionPercent,
    powerPercent,
    soulAttackIncreasePercent,
    passiveAttackPercent,
    passiveSkillName,
    passiveSkillLevel,
    percentPerLevel: jobPassive ? jobPassive.percentPerLevel : 0
  };
}

function calculateCriticalHit(equipment, accessories, statData, daevanionData) {
  
  let totalCriticalHitInteger = 0; // 총 치명타 정수
  let totalCriticalHitPercent = 0; // 총 치명타 퍼센트
  
  // Breakdown 정보 추적
  let baseCriticalHitInteger = 0; // 기본 옵션 치명타 (정수)
  let soulCriticalHitInteger = 0; // 영혼 각인 치명타 (정수)
  let stoneCriticalHitInteger = 0; // 마석 각인 치명타 (정수)
  let daevanionCriticalHitInteger = 0; // 데바니온 치명타 (정수)
  let intelligentPetCriticalMin = 0; // 지성 펫작 치명타 (최소)
  let intelligentPetCriticalMax = 0; // 지성 펫작 치명타 (최대)
  let deathCriticalHitPercent = 0; // 주신 스탯 죽음으로 인한 치명타 (퍼센트)
  let accuracyCriticalHitPercent = 0; // 일반 스탯 정확으로 인한 치명타 (퍼센트)
  
  // 1. 메인 무기/가더 기본 옵션에서 치명타 추출 (정수)
  // calculateAttackPower와 동일한 방식으로 slotPos 사용 (1=메인 무기, 2=가더)
  const weaponAndGauntlet = [...(equipment || [])].filter(item => {
    // slotPos, slot_pos, slot_index, slot, raw_data.slotPos 등 다양한 경로 확인
    let slotPos = -1;
    
    if (item.slotPos !== undefined && item.slotPos !== null) slotPos = item.slotPos;
    else if (item.slot_pos !== undefined && item.slot_pos !== null) slotPos = item.slot_pos;
    else if (item.slot_index !== undefined && item.slot_index !== null) slotPos = item.slot_index;
    else if (item.slot !== undefined && item.slot !== null) slotPos = item.slot;
    else if (item.raw_data && item.raw_data.slotPos !== undefined && item.raw_data.slotPos !== null) slotPos = item.raw_data.slotPos;
    
    // slotPos가 1이면 메인 무기, 2이면 가더
    // 또한 slot_index가 0이면 무기, 1이면 가더 (하위 호환성)
    return slotPos == 1 || slotPos == 2 || slotPos == 0 || slotPos == '0' || slotPos == '1' || slotPos == '2';
  });
  
  
  weaponAndGauntlet.forEach((item, index) => {
    const itemName = item.name || '알 수 없음';
    
    // mainStats에서 치명타 추출
    if (item.main_stats) {
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach((stat, statIndex) => {
          if (typeof stat === 'object' && stat !== null) {
            const statName = String(stat.name || stat.id || '').toLowerCase();
            const statValue = stat.value || stat.minValue || '';
            
            // "치명타"가 포함되어 있는지 확인 (치명타 저항, 치명타 피해 증폭 등 제외)
            const hasCritical = (statName.includes('치명타') || statName.includes('critical')) &&
                                !statName.includes('치명타 방어력') && 
                                !statName.includes('치명타 저항') && 
                                !statName.includes('치명타 피해 증폭') &&
                                !statName.includes('critical resistance') && 
                                !statName.includes('critical damage');
            
            if (hasCritical) {
              let criticalValue = 0;
              
              // value가 문자열인 경우 (예: "100", "100 (+50)" 등)
              if (typeof statValue === 'string') {
                // 숫자만 추출
                const numMatch = statValue.match(/(\d+)/);
                if (numMatch) {
                  criticalValue = parseInt(numMatch[1]) || 0;
                }
              } else if (typeof statValue === 'number') {
                criticalValue = parseInt(statValue) || 0;
              }
              
              if (criticalValue > 0) {
                baseCriticalHitInteger += criticalValue;
                totalCriticalHitInteger += criticalValue;
              }
            }
          }
        });
      } else if (typeof item.main_stats === 'object' && !Array.isArray(item.main_stats)) {
        for (const key in item.main_stats) {
          const keyLower = String(key).toLowerCase();
          const value = item.main_stats[key];
          
          // "치명타"가 포함되어 있는지 확인 (치명타 저항, 치명타 피해 증폭 등 제외)
          const hasCritical = (keyLower.includes('치명타') || keyLower.includes('critical')) &&
                              !keyLower.includes('치명타 방어력') && 
                              !keyLower.includes('치명타 저항') && 
                              !keyLower.includes('치명타 피해 증폭') &&
                              !keyLower.includes('critical resistance') && 
                              !keyLower.includes('critical damage');
          
          if (hasCritical) {
            let criticalValue = 0;
            
            if (typeof value === 'string') {
              const numMatch = value.match(/(\d+)/);
              if (numMatch) {
                criticalValue = parseInt(numMatch[1]) || 0;
              }
            } else if (typeof value === 'number') {
              criticalValue = parseInt(value) || 0;
            }
            
            if (criticalValue > 0) {
              baseCriticalHitInteger += criticalValue;
              totalCriticalHitInteger += criticalValue;
            }
          }
        }
      }
    }
  });
  
  // 2. 영혼 각인 및 마석 각인에서 치명타 추출 (정수)
  const allItems = [...(equipment || []), ...(accessories || [])];
  
  allItems.forEach((item, itemIndex) => {
    const itemName = item.name || '알 수 없음';
    
    // subStats (영혼 각인)에서 치명타 추출
    if (item.sub_stats) {
      
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach((stat, statIndex) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            
            // 정확히 "치명타"만 매칭 (치명타 저항, 치명타 피해 증폭 등 제외)
            if (value > 0 && 
                ((name === '치명타' || name === 'critical') || 
                 (name.startsWith('치명타 ') && !name.includes('치명타 방어력') && !name.includes('치명타 저항') && !name.includes('치명타 피해 증폭')) ||
                 (name.startsWith('critical ') && !name.includes('critical resistance') && !name.includes('critical damage')))) {
              soulCriticalHitInteger += value;
              totalCriticalHitInteger += value;
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseInt(item.sub_stats[key] || 0);
          
          
          // 정확히 "치명타"만 매칭 (치명타 저항, 치명타 피해 증폭 등 제외)
          if (value > 0 && 
              ((keyLower === '치명타' || keyLower === 'critical') || 
               (keyLower.startsWith('치명타 ') && !keyLower.includes('치명타 방어력') && !keyLower.includes('치명타 저항') && !keyLower.includes('치명타 피해 증폭')) ||
               (keyLower.startsWith('critical ') && !keyLower.includes('critical resistance') && !keyLower.includes('critical damage')))) {
            soulCriticalHitInteger += value;
            totalCriticalHitInteger += value;
          }
        }
      }
    } else {
    }
    
    // magic_stone_stat (마석 각인)에서 치명타 추출
    if (item.magic_stone_stat) {
      
      if (Array.isArray(item.magic_stone_stat)) {
        item.magic_stone_stat.forEach((stat, statIndex) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            
            // 정확히 "치명타"만 매칭 (치명타 저항, 치명타 피해 증폭 등 제외)
            if (value > 0 && 
                ((name === '치명타' || name === 'critical') || 
                 (name.startsWith('치명타 ') && !name.includes('치명타 방어력') && !name.includes('치명타 저항') && !name.includes('치명타 피해 증폭')) ||
                 (name.startsWith('critical ') && !name.includes('critical resistance') && !name.includes('critical damage')))) {
              stoneCriticalHitInteger += value;
              totalCriticalHitInteger += value;
            }
          }
        });
      } else if (typeof item.magic_stone_stat === 'object') {
        for (const key of Object.keys(item.magic_stone_stat)) {
          const keyLower = key.toLowerCase();
          const value = parseInt(item.magic_stone_stat[key] || 0);
          
          
          // 정확히 "치명타"만 매칭 (치명타 저항, 치명타 피해 증폭 등 제외)
          if (value > 0 && 
              ((keyLower === '치명타' || keyLower === 'critical') || 
               (keyLower.startsWith('치명타 ') && !keyLower.includes('치명타 방어력') && !keyLower.includes('치명타 저항') && !keyLower.includes('치명타 피해 증폭')) ||
               (keyLower.startsWith('critical ') && !keyLower.includes('critical resistance') && !keyLower.includes('critical damage')))) {
            stoneCriticalHitInteger += value;
            totalCriticalHitInteger += value;
          }
        }
      }
    } else {
    }
  });
  
  // 3. 데바니온 (아리엘, 아스펠 제외) 5개 보드에서 치명타 추출 (정수)
  const daevanionBoardIds = [41, 42, 43, 44, 47]; // 네자칸, 지켈, 바이젤, 트리니엘, 마르쿠탄
  const daevanionBoardNames = { 41: '네자칸', 42: '지켈', 43: '바이젤', 44: '트리니엘', 47: '마르쿠탄' };
  let daevanionMarkutanCriticalValue = 0; // 마르쿠탄 치명타 별도 추적
  
  if (daevanionData) {
    
    daevanionBoardIds.forEach(boardId => {
      const boardName = daevanionBoardNames[boardId] || `보드${boardId}`;
      const boardData = daevanionData[boardId];
      let boardCriticalHit = 0;
      
      if (boardData && boardData.nodeList) {
        const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
        
        activeNodes.forEach((node, nodeIndex) => {
          let nodeCriticalHit = 0;
          let foundInField = null;
          
          // 처음 몇 개 노드의 구조 확인
          if (nodeIndex < 3) {
          }
          
          // 노드의 모든 필드를 순회하며 검색 - 공격력 계산과 동일한 방식
          for (const key in node) {
            const value = node[key];
            
            // 문자열인 경우 패턴 검색
            if (typeof value === 'string' && value.trim()) {
              const text = value;
              
              // "치명타 +X" 패턴 검색 (치명타 저항, 치명타 피해 증폭 등 제외)
              let matches = text.match(/치명타\s*[+＋]\s*(\d+)/gi);
              if (!matches) {
                matches = text.match(/critical\s*[+＋]\s*(\d+)/gi);
              }
              
              if (matches) {
                matches.forEach(match => {
                  // 치명타 저항, 치명타 피해 증폭 등 제외
                  const matchLower = match.toLowerCase();
                  if (matchLower.includes('치명타 저항') || matchLower.includes('치명타 방어력') || matchLower.includes('치명타 피해 증폭') ||
                      matchLower.includes('critical resistance') || matchLower.includes('critical damage')) {
                    return; // 이 매칭은 건너뜀
                  }
                  
                  const numMatch = match.match(/(\d+)/);
                  if (numMatch) {
                    const criticalValue = parseInt(numMatch[1]) || 0;
                    if (criticalValue > 0) {
                      nodeCriticalHit += criticalValue;
                      if (!foundInField) foundInField = key;
                    }
                  }
                });
              }
            }
            
            // 배열인 경우 (statList, effectList 등)
            if (Array.isArray(value) && value.length > 0) {
              value.forEach((item, itemIndex) => {
                if (typeof item === 'object' && item !== null) {
                  // 객체의 모든 필드 검색
                  for (const itemKey in item) {
                    const itemValue = item[itemKey];
                    if (typeof itemValue === 'string' && itemValue.trim()) {
                      let matches = itemValue.match(/치명타\s*[+＋]\s*(\d+)/gi);
                      if (!matches) {
                        matches = itemValue.match(/critical\s*[+＋]\s*(\d+)/gi);
                      }
                      
                      if (matches) {
                        matches.forEach(match => {
                          // 치명타 저항, 치명타 피해 증폭 등 제외
                          const matchLower = match.toLowerCase();
                          if (matchLower.includes('치명타 저항') || matchLower.includes('치명타 방어력') || matchLower.includes('치명타 피해 증폭') ||
                              matchLower.includes('critical resistance') || matchLower.includes('critical damage')) {
                            return; // 이 매칭은 건너뜀
                          }
                          
                          const numMatch = match.match(/(\d+)/);
                          if (numMatch) {
                            const criticalValue = parseInt(numMatch[1]) || 0;
                            if (criticalValue > 0) {
                              nodeCriticalHit += criticalValue;
                              if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                            }
                          }
                        });
                      }
                    }
                    
                    // value나 amount 필드가 숫자인 경우
                    if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                      const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                      // 정확히 "치명타"만 매칭 (치명타 저항, 치명타 피해 증폭 등 제외)
                      if ((itemName === '치명타' || itemName === 'critical') || 
                          (itemName.startsWith('치명타 ') && !itemName.includes('치명타 방어력') && !itemName.includes('치명타 저항') && !itemName.includes('치명타 피해 증폭')) ||
                          (itemName.startsWith('critical ') && !itemName.includes('critical resistance') && !itemName.includes('critical damage'))) {
                        nodeCriticalHit += itemValue;
                        if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                      }
                    }
                  }
                }
              });
            }
            
            // 객체인 경우 재귀적으로 검색
            if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
              for (const subKey in value) {
                const subValue = value[subKey];
                if (typeof subValue === 'string' && subValue.trim()) {
                  let matches = subValue.match(/치명타\s*[+＋]\s*(\d+)/gi);
                  if (!matches) {
                    matches = subValue.match(/critical\s*[+＋]\s*(\d+)/gi);
                  }
                  
                  if (matches) {
                    matches.forEach(match => {
                      // 치명타 저항, 치명타 피해 증폭 등 제외
                      const matchLower = match.toLowerCase();
                      if (matchLower.includes('치명타 저항') || matchLower.includes('치명타 방어력') || matchLower.includes('치명타 피해 증폭') ||
                          matchLower.includes('critical resistance') || matchLower.includes('critical damage')) {
                        return; // 이 매칭은 건너뜀
                      }
                      
                      const numMatch = match.match(/(\d+)/);
                      if (numMatch) {
                        const criticalValue = parseInt(numMatch[1]) || 0;
                        if (criticalValue > 0) {
                          nodeCriticalHit += criticalValue;
                          if (!foundInField) foundInField = `${key}.${subKey}`;
                        }
                      }
                    });
                  }
                }
              }
            }
          }
          
          // NC API 오류로 숫자가 누락된 경우 처리 (활성화된 "치명타" 노드인데 숫자가 없는 경우)
          // "치명타" 노드는 기본값 +10
          if (nodeCriticalHit === 0) {
            const nodeName = node.name || node.desc || node.effect || '';
            const nodeText = String(nodeName).trim();
            // "치명타"가 포함되어 있지만, 치명타 저항, 치명타 피해 증폭, 치명타 방어력 등은 제외
            if (nodeText.includes('치명타') && 
                !nodeText.includes('치명타 저항') && !nodeText.includes('치명타 방어력') && 
                !nodeText.includes('치명타 피해') && !nodeText.includes('저항')) {
              nodeCriticalHit = 10; // 기본값 +10
              foundInField = 'API누락-기본값적용';
            }
          }
          
          if (nodeCriticalHit > 0) {
            boardCriticalHit += nodeCriticalHit;
            const nodeName = node.name || node.desc || node.effect || `노드${nodeIndex}`;
          }
        });
        
        if (boardCriticalHit > 0) {
          daevanionCriticalHitInteger += boardCriticalHit;
          totalCriticalHitInteger += boardCriticalHit;
          if (boardId === 47) {
            daevanionMarkutanCriticalValue = boardCriticalHit; // 마르쿠탄 별도 추적
          }
        }
      }
    });
  }
  
  // 정수 합계를 퍼센트로 변환 (정수 1당 0.1%, 즉 정수 * 0.1)
  const integerToPercent = totalCriticalHitInteger * 0.1;
  
  // 4. 주신 스탯 중 죽음[트리니엘] 스탯 1당 치명타 +0.1%
  if (statData && statData.statList) {
    
    const deathStat = statData.statList.find(stat => 
      stat.type === 'Death' || (stat.name && (stat.name.includes('죽음') || stat.name.includes('Death') || stat.name.includes('트리니엘')))
    );
    
    if (deathStat) {
      const deathValue = parseInt(deathStat.value || 0);
      deathCriticalHitPercent = deathValue * 0.2; // 스탯 1당 0.2%, 2배 적용
      totalCriticalHitPercent += deathCriticalHitPercent;
    } else {
    }
  } else {
  }
  
  // 5. 일반 스탯 중 정확 스탯 1당 치명타 +0.1% (캡 200 적용)
  let accuracyValue = 0; // 정확 값
  
  if (statData && statData.statList) {
    const accuracyStat = statData.statList.find(stat => 
      stat.type === 'Accuracy' || (stat.name && stat.name.includes('정확'))
    );
    if (accuracyStat) {
      accuracyValue = parseInt(accuracyStat.value || 0);
      const cappedAccuracy = Math.min(accuracyValue, 200); // 정확 캡 200 적용
      
      accuracyCriticalHitPercent = cappedAccuracy * 0.1;
      totalCriticalHitPercent += accuracyCriticalHitPercent;
    } else {
    }
  }
  
  // 날개 장착 효과로 인한 치명타 추가
  let wingCriticalHitInteger = 0;
  const wingEffects = getWingEffects();
  if (wingEffects && wingEffects.criticalHit) {
    wingCriticalHitInteger = wingEffects.criticalHit;
    totalCriticalHitInteger += wingCriticalHitInteger;
  }
  
  // 타이틀 장착 효과에서 치명타 추출 ("치명타"만, "치명타 공격력"이나 "치명타 피해 증폭" 제외)
  let titleEquipCriticalHit = 0;
  const currentTitles = window.currentTitles || [];
  if (currentTitles && Array.isArray(currentTitles)) {
    currentTitles.forEach((title) => {
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          if (typeof effect === 'string') {
            // "치명타 +100" 패턴 매칭 (정확히 "치명타"만, "치명타 공격력"이나 "치명타 피해 증폭" 제외)
            const criticalMatch = effect.match(/^치명타\s*\+?(\d+)/i);
            if (criticalMatch) {
              titleEquipCriticalHit += parseInt(criticalMatch[1]) || 0;
            }
          }
        });
      }
    });
  }
  totalCriticalHitInteger += titleEquipCriticalHit;
  
  // 지성 펫작으로 인한 치명타 (서버에서 계산된 값 사용 - 야성 펫작 명중과 동일한 로직)
  const purePower = window.currentCharacterData?.pure_power || 0;
  intelligentPetCriticalMin = window.currentCharacterData?.intelligent_pet_critical_min || 0;
  intelligentPetCriticalMax = window.currentCharacterData?.intelligent_pet_critical_max || 41;
  
  // 최종 치명타 퍼센트 = 정수 합계 * 0.1% + 주신 스탯 퍼센트 + 일반 스탯 퍼센트
  const integerToPercentWithWing = totalCriticalHitInteger * 0.1; // 날개 포함
  const finalCriticalHitPercent = integerToPercentWithWing + totalCriticalHitPercent;
  
  
  // 각 정수 소스를 퍼센트로 변환 (정수 1당 0.1%)
  const soulCriticalHitPercent = soulCriticalHitInteger * 0.1;
  const stoneCriticalHitPercent = stoneCriticalHitInteger * 0.1;
  const daevanionCriticalHitPercent = daevanionCriticalHitInteger * 0.1;
  const wingCriticalHitPercent = wingCriticalHitInteger * 0.1;
  
  const result = {
    totalCriticalHitPercent: finalCriticalHitPercent,
    totalCriticalHitInteger: totalCriticalHitInteger,
    breakdown: {
      baseCriticalHitInteger: baseCriticalHitInteger,
      soulCriticalHitInteger: soulCriticalHitInteger,
      soulCriticalHitPercent: soulCriticalHitPercent,
      stoneCriticalHitInteger: stoneCriticalHitInteger,
      stoneCriticalHitPercent: stoneCriticalHitPercent,
      daevanionCriticalHitInteger: daevanionCriticalHitInteger,
      daevanionMarkutanCriticalHitInteger: daevanionMarkutanCriticalValue,
      daevanionCriticalHitPercent: daevanionCriticalHitPercent,
      intelligentPetCriticalMin: intelligentPetCriticalMin,
      intelligentPetCriticalMax: intelligentPetCriticalMax,
      deathCriticalHitPercent: deathCriticalHitPercent,
      accuracyCriticalHitPercent: accuracyCriticalHitPercent,
      wingCriticalHitInteger: wingCriticalHitInteger,
      wingCriticalHitPercent: wingCriticalHitPercent,
      titleEquipCriticalHit: titleEquipCriticalHit,
      wingName: wingEffects ? wingEffects.name : null,
      purePower: purePower  // 지성 펫작 완료율 계산용
    }
  };
  
  // 전역 변수에 저장
  window.criticalHitResult = result;
  
  // 치명타 표시 업데이트
  displayCriticalHitStats(result);
  
  return result;
}

function calculateCriticalAttackPowerBonus(criticalChancePercent) {
  const wingEffects = getWingEffects();
  if (wingEffects && wingEffects.criticalAttackPower) {
    // 치명타 확률이 퍼센트로 제공됨 (예: 65.5 → 0.655)
    return wingEffects.criticalAttackPower * (criticalChancePercent / 100);
  }
  return 0;
}

function calculateDamageAmplification(equipment, accessories, daevanionData, titles) {
  
  // 4가지 피해 증폭을 각각 추적
  // 1. 무기 피해 증폭
  let weaponDamageAmpPercent = 0;
  let weaponDamageAmpInteger = 0;
  let weaponSoulPercent = 0;
  let weaponEquipmentBasePercent = 0;
  let weaponStoneInteger = 0;
  let weaponDaevanionPercent = 0;
  let weaponDaevanionArielPercent = 0;
  let weaponTitlePercent = 0;
  
  // 2. PVE 피해 증폭
  let pveDamageAmpPercent = 0;
  let pveDamageAmpInteger = 0;
  let pveSoulPercent = 0;
  let pveEquipmentBasePercent = 0;
  let pveStoneInteger = 0;
  let pveDaevanionPercent = 0;
  let pveDaevanionArielPercent = 0;
  let pveTitlePercent = 0;
  
  // 3. 피해 증폭 (일반)
  let damageAmpPercent = 0;
  let damageAmpInteger = 0;
  let damageAmpSoulPercent = 0;
  let damageAmpEquipmentBasePercent = 0;
  let damageAmpStoneInteger = 0;
  let damageAmpDaevanionPercent = 0;
  let damageAmpDaevanionArielPercent = 0;
  let damageAmpTitlePercent = 0;
  
  // 4. 치명타 피해 증폭
  let criticalDamageAmpPercent = 0;
  let criticalDamageAmpInteger = 0;
  let criticalSoulPercent = 0;
  let criticalEquipmentBasePercent = 0;
  let criticalStoneInteger = 0;
  let criticalDaevanionPercent = 0;
  let criticalDaevanionArielPercent = 0;
  let criticalTitlePercent = 0;
  
  // 5. 아르카나 세트 효과
  let arcanaPurityCriticalDamageAmpPercent = 0; // 순수 4세트: 치명타 피해 증폭 5%
  let arcanaFrenzyDamageAmpPercent = 0; // 광분 4세트: 보스 피해 증폭 5%
  
  // 키워드 정의
  const weaponDamageAmpKeywords = ['무기 피해 증폭', '무기피해증폭', 'weapon damage amplification'];
  const pveDamageAmpKeywords = ['pve 피해 증폭', 'pve피해증폭', 'pve damage amplification'];
  const damageAmpKeywords = ['피해 증폭', 'damage amplification'];
  const criticalDamageAmpKeywords = ['치명타 피해 증폭', '치명타피해증폭', 'critical damage amplification'];
  
  // 장비 기본 옵션용 키워드
  const equipmentBaseWeaponKeywords = ['무기 피해 증폭', '무기피해증폭', 'weapon damage amplification'];
  const equipmentBasePveKeywords = ['pve 피해 증폭', 'pve피해증폭', 'pve damage amplification'];
  
  // 1. 장비 영혼 각인에서 피해 증폭 추출 (퍼센트) - 공격력 계산과 동일한 방식
  const allItems = [...(equipment || []), ...(accessories || [])];
  
  // PVP 장비 세트 디버프 계산 (십부장/백부장/천부장/군단장)
  // 주의: 메인 무기(equipment[0])는 세트 카운트에서 제외, 가더(equipment[1])는 포함
  const pvpGearCounts = {
    sip: 0, // 십부장
    baek: 0, // 백부장
    cheon: 0, // 천부장
    gundan: 0 // 군단장
  };
  allItems.forEach((item, index) => {
    const itemName = item?.name || '';
    if (!itemName) return;
    
    // 메인 무기(equipment 배열의 첫 번째 = allItems의 0번 인덱스)는 PVP 세트 카운트에서 제외
    const isMainWeapon = index === 0 && equipment && equipment.length > 0;
    if (isMainWeapon) return;
    
    if (itemName.includes('십부장')) pvpGearCounts.sip += 1;
    if (itemName.includes('백부장')) pvpGearCounts.baek += 1;
    if (itemName.includes('천부장')) pvpGearCounts.cheon += 1;
    if (itemName.includes('군단장')) pvpGearCounts.gundan += 1;
  });
  
  const getPvpGearDebuffPercent = (count) => {
    if (count >= 12) return -20;
    if (count >= 8) return -15;
    if (count >= 5) return -10;
    if (count >= 2) return -5;
    return 0;
  };
  
  const pvpGearDebuffByRank = {
    sip: getPvpGearDebuffPercent(pvpGearCounts.sip),
    baek: getPvpGearDebuffPercent(pvpGearCounts.baek),
    cheon: getPvpGearDebuffPercent(pvpGearCounts.cheon),
    gundan: getPvpGearDebuffPercent(pvpGearCounts.gundan)
  };
  const pvpGearDebuffPercent = pvpGearDebuffByRank.sip + pvpGearDebuffByRank.baek + pvpGearDebuffByRank.cheon + pvpGearDebuffByRank.gundan;
  
  allItems.forEach((item, itemIndex) => {
    const itemName = item.name || '알 수 없음';
    
    // subStats (영혼 각인)에서 피해 증폭 추출 - 4가지 피해 증폭을 각각 추적
    if (item.sub_stats) {
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach((stat, statIndex) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseFloat(stat.value || stat.amount || 0);
            
            // 4가지 피해 증폭을 구분하여 추출
            if (value > 0) {
              if (weaponDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                weaponSoulPercent += value;
                weaponDamageAmpPercent += value;
              } else if (pveDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                pveSoulPercent += value;
                pveDamageAmpPercent += value;
              } else if (criticalDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                criticalSoulPercent += value;
                criticalDamageAmpPercent += value;
              } else if (damageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                // "피해 증폭"은 다른 키워드와 겹치지 않을 때만 (예: "무기 피해 증폭"이 아닐 때)
                if (!name.includes('무기') && !name.includes('pve') && !name.includes('치명타') && !name.includes('weapon') && !name.includes('critical')) {
                  damageAmpSoulPercent += value;
                  damageAmpPercent += value;
                }
              }
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseFloat(item.sub_stats[key] || 0);
          
          // 4가지 피해 증폭을 구분하여 추출
          if (value > 0) {
            if (weaponDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              weaponSoulPercent += value;
              weaponDamageAmpPercent += value;
            } else if (pveDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              pveSoulPercent += value;
              pveDamageAmpPercent += value;
            } else if (criticalDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              criticalSoulPercent += value;
              criticalDamageAmpPercent += value;
            } else if (damageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              // "피해 증폭"은 다른 키워드와 겹치지 않을 때만
              if (!keyLower.includes('무기') && !keyLower.includes('pve') && !keyLower.includes('치명타') && !keyLower.includes('weapon') && !keyLower.includes('critical')) {
                damageAmpSoulPercent += value;
                damageAmpPercent += value;
              }
            }
          }
        }
      }
    }
    
    // main_stats (기본 옵션)에서 피해 증폭 추출 (퍼센트) - 4가지 피해 증폭을 각각 추적
    if (item.main_stats) {
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach((stat) => {
          if (typeof stat === 'object' && stat !== null) {
            const name = (stat.name || stat.type || '').toLowerCase();
            const statValue = stat.value || stat.minValue || '';
            const statExtra = stat.extra || '';
            
            // 키워드 매칭 확인 및 값 추출
            let totalValue = 0;
            
            // value에서 기본 값 추출
            if (typeof statValue === 'string') {
              // "0.5% (+1.5%)" 또는 "0.5 (+1.5)" 형태 파싱
              const match = statValue.match(/(\d+\.?\d*)\s*%?\s*\(\+\s*(\d+\.?\d*)\s*%?\)/);
              if (match) {
                totalValue = parseFloat(match[1]) || 0;
                const bonusValue = parseFloat(match[2]) || 0;
                totalValue += bonusValue;
              } else {
                // 괄호가 없는 경우 일반 파싱
                const numMatch = statValue.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  totalValue = parseFloat(numMatch[1]) || 0;
                }
              }
            } else {
              // 숫자인 경우
              totalValue = parseFloat(statValue) || 0;
            }
            
            // extra에서 추가 값 추출 (공격력 계산과 동일한 방식)
            if (statExtra && statExtra !== '0' && statExtra !== 0 && statExtra !== '0%') {
              if (typeof statExtra === 'string') {
                // "+1.5%" 또는 "+1.5" 형태 파싱
                const extraMatch = statExtra.match(/\+?\s*(\d+\.?\d*)\s*%?/);
                if (extraMatch) {
                  const extraValue = parseFloat(extraMatch[1]) || 0;
                  totalValue += extraValue;
                }
              } else {
                // 숫자인 경우
                const extraValue = parseFloat(statExtra) || 0;
                totalValue += extraValue;
              }
            }
            
            // 4가지 피해 증폭을 구분하여 추가
            if (totalValue > 0) {
              if (equipmentBaseWeaponKeywords.some(keyword => name.includes(keyword))) {
                weaponEquipmentBasePercent += totalValue;
                weaponDamageAmpPercent += totalValue;
              } else if (equipmentBasePveKeywords.some(keyword => name.includes(keyword))) {
                pveEquipmentBasePercent += totalValue;
                pveDamageAmpPercent += totalValue;
              }
            }
          }
        });
      } else if (typeof item.main_stats === 'object') {
        for (const key of Object.keys(item.main_stats)) {
          const keyLower = key.toLowerCase();
          const rawValue = item.main_stats[key];
          
          // 키워드 매칭 확인 및 값 추출
          let totalValue = 0;
          
          // 값이 문자열인 경우 괄호 안의 값도 파싱 (예: "0.5% (+1.5%)")
          if (typeof rawValue === 'string') {
            // "0.5% (+1.5%)" 또는 "0.5 (+1.5)" 형태 파싱
            const match = rawValue.match(/(\d+\.?\d*)\s*%?\s*\(\+\s*(\d+\.?\d*)\s*%?\)/);
            if (match) {
              totalValue = parseFloat(match[1]) || 0;
              const bonusValue = parseFloat(match[2]) || 0;
              totalValue += bonusValue;
            } else {
              // 괄호가 없는 경우 일반 파싱
              const numMatch = rawValue.match(/(\d+\.?\d*)/);
              if (numMatch) {
                totalValue = parseFloat(numMatch[1]) || 0;
              }
            }
          } else {
            // 숫자인 경우
            totalValue = parseFloat(rawValue) || 0;
          }
          
          // extra 필드가 별도로 있는 경우 확인
          const extraKey = key + '_extra';
          if (item.main_stats[extraKey]) {
            const extraValue = item.main_stats[extraKey];
            if (typeof extraValue === 'string') {
              const extraMatch = extraValue.match(/\+?\s*(\d+\.?\d*)\s*%?/);
              if (extraMatch) {
                totalValue += parseFloat(extraMatch[1]) || 0;
              }
            } else {
              totalValue += parseFloat(extraValue) || 0;
            }
          }
          
          // 4가지 피해 증폭을 구분하여 추가
          if (totalValue > 0) {
            if (equipmentBaseWeaponKeywords.some(keyword => keyLower.includes(keyword))) {
              weaponEquipmentBasePercent += totalValue;
              weaponDamageAmpPercent += totalValue;
            } else if (equipmentBasePveKeywords.some(keyword => keyLower.includes(keyword))) {
              pveEquipmentBasePercent += totalValue;
              pveDamageAmpPercent += totalValue;
            }
          }
        }
      }
    }
    
    // magic_stone_stat (마석 각인)에서 피해 증폭 추출 (정수) - 4가지 피해 증폭을 각각 추적
    if (item.magic_stone_stat) {
      if (Array.isArray(item.magic_stone_stat)) {
        item.magic_stone_stat.forEach((stat, statIndex) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            // 4가지 피해 증폭을 구분하여 추출
            if (value > 0) {
              if (weaponDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                weaponStoneInteger += value;
                weaponDamageAmpInteger += value;
              } else if (pveDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                pveStoneInteger += value;
                pveDamageAmpInteger += value;
              } else if (criticalDamageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                criticalStoneInteger += value;
                criticalDamageAmpInteger += value;
              } else if (damageAmpKeywords.some(keyword => name.includes(keyword.toLowerCase()))) {
                // "피해 증폭"은 다른 키워드와 겹치지 않을 때만
                if (!name.includes('무기') && !name.includes('pve') && !name.includes('치명타') && !name.includes('weapon') && !name.includes('critical')) {
                  damageAmpStoneInteger += value;
                  damageAmpInteger += value;
                }
              }
            }
          }
        });
      } else if (typeof item.magic_stone_stat === 'object') {
        for (const key of Object.keys(item.magic_stone_stat)) {
          const keyLower = key.toLowerCase();
          const value = parseInt(item.magic_stone_stat[key] || 0);
          
          // 4가지 피해 증폭을 구분하여 추출
          if (value > 0) {
            if (weaponDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              weaponStoneInteger += value;
              weaponDamageAmpInteger += value;
            } else if (pveDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              pveStoneInteger += value;
              pveDamageAmpInteger += value;
            } else if (criticalDamageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              criticalStoneInteger += value;
              criticalDamageAmpInteger += value;
            } else if (damageAmpKeywords.some(keyword => keyLower.includes(keyword.toLowerCase()))) {
              // "피해 증폭"은 다른 키워드와 겹치지 않을 때만
              if (!keyLower.includes('무기') && !keyLower.includes('pve') && !keyLower.includes('치명타') && !keyLower.includes('weapon') && !keyLower.includes('critical')) {
                damageAmpStoneInteger += value;
                damageAmpInteger += value;
              }
            }
          }
        }
      }
    }
  });
  
  // 2. 데바니온 지켈(42), 바이젤(43), 마르쿠탄(47) 보드에서 피해 증폭 추출
  // 지켈(42): 무기 피해 증폭, PVE 피해 증폭, 피해 증폭
  // 바이젤(43): 치명타 피해 증폭
  // 마르쿠탄(47): 무기 피해 증폭
  const daevanionBoardIds = [42, 43, 47]; // 지켈, 바이젤, 마르쿠탄
  const daevanionBoardNames = { 42: '지켈', 43: '바이젤', 47: '마르쿠탄' };
  
  if (daevanionData) {
    
    daevanionBoardIds.forEach(boardId => {
      const boardName = daevanionBoardNames[boardId] || `보드${boardId}`;
      const boardData = daevanionData[boardId];
      const isZikel = (boardId === 42);  // 지켈 보드
      const isVaizel = (boardId === 43); // 바이젤 보드
      const isMarkutan = (boardId === 47); // 마르쿠탄 보드
      
      let boardWeaponAmp = 0;
      let boardPveAmp = 0;
      let boardDamageAmp = 0;
      let boardCriticalAmp = 0;
      
      if (boardData && boardData.nodeList) {
        const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
        activeNodes.forEach((node, nodeIndex) => {
          let nodeWeaponAmp = 0;
          let nodePveAmp = 0;
          let nodeDamageAmp = 0;
          let nodeCriticalAmp = 0;
          let foundInField = null;
          
          // 노드의 모든 필드를 순회하며 검색
          for (const key in node) {
            const value = node[key];
            
            // 문자열인 경우 패턴 검색
            if (typeof value === 'string' && value.trim()) {
              const text = value;
              
              // 지켈에서는: 무기 피해 증폭, PVE 피해 증폭, 피해 증폭 검색
              if (isZikel) {
                // 무기 피해 증폭 패턴 검색
                let weaponMatches = text.match(/무기\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!weaponMatches) {
                  weaponMatches = text.match(/무기피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!weaponMatches) {
                  weaponMatches = text.match(/weapon\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (weaponMatches) {
                  weaponMatches.forEach(match => {
                    const numMatch = match.match(/(\d+\.?\d*)/);
                    if (numMatch) {
                      const ampValue = parseFloat(numMatch[1]) || 0;
                      if (ampValue > 0) {
                        nodeWeaponAmp += ampValue;
                        if (!foundInField) foundInField = key;
                      }
                    }
                  });
                }
                
                // PVE 피해 증폭 패턴 검색
                let pveMatches = text.match(/pve\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!pveMatches) {
                  pveMatches = text.match(/pve피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!pveMatches) {
                  pveMatches = text.match(/pve\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (pveMatches) {
                  pveMatches.forEach(match => {
                    const numMatch = match.match(/(\d+\.?\d*)/);
                    if (numMatch) {
                      const ampValue = parseFloat(numMatch[1]) || 0;
                      if (ampValue > 0) {
                        nodePveAmp += ampValue;
                        if (!foundInField) foundInField = key;
                      }
                    }
                  });
                }
                
                // 피해 증폭 패턴 검색 (다른 키워드와 겹치지 않을 때만)
                let damageMatches = text.match(/피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!damageMatches) {
                  damageMatches = text.match(/피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!damageMatches) {
                  damageMatches = text.match(/damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (damageMatches) {
                  damageMatches.forEach(match => {
                    const matchLower = match.toLowerCase();
                    // 다른 키워드와 겹치지 않을 때만 추가
                    if (!matchLower.includes('무기') && !matchLower.includes('pve') && !matchLower.includes('치명타') && !matchLower.includes('weapon') && !matchLower.includes('critical')) {
                      const numMatch = match.match(/(\d+\.?\d*)/);
                      if (numMatch) {
                        const ampValue = parseFloat(numMatch[1]) || 0;
                        if (ampValue > 0) {
                          nodeDamageAmp += ampValue;
                          if (!foundInField) foundInField = key;
                        }
                      }
                    }
                  });
                }
              }
              
              // 바이젤에서는: 치명타 피해 증폭만 검색
              if (isVaizel) {
                // 치명타 피해 증폭 패턴 검색
                let criticalMatches = text.match(/치명타\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!criticalMatches) {
                  criticalMatches = text.match(/치명타피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!criticalMatches) {
                  criticalMatches = text.match(/critical\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (criticalMatches) {
                  criticalMatches.forEach(match => {
                    const numMatch = match.match(/(\d+\.?\d*)/);
                    if (numMatch) {
                      const ampValue = parseFloat(numMatch[1]) || 0;
                      if (ampValue > 0) {
                        nodeCriticalAmp += ampValue;
                        if (!foundInField) foundInField = key;
                      }
                    }
                  });
                }
              }
              
              // 마르쿠탄에서는: 무기 피해 증폭 검색
              if (isMarkutan) {
                let weaponMatches = text.match(/무기\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!weaponMatches) {
                  weaponMatches = text.match(/무기피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!weaponMatches) {
                  weaponMatches = text.match(/weapon\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (weaponMatches) {
                  weaponMatches.forEach(match => {
                    const numMatch = match.match(/(\d+\.?\d*)/);
                    if (numMatch) {
                      const ampValue = parseFloat(numMatch[1]) || 0;
                      if (ampValue > 0) {
                        nodeWeaponAmp += ampValue;
                        if (!foundInField) foundInField = key;
                      }
                    }
                  });
                }
              }
            }
            
            // 배열인 경우 (statList, effectList 등) - 4가지 피해 증폭을 각각 추적
            if (Array.isArray(value) && value.length > 0) {
              value.forEach((item, itemIndex) => {
                if (typeof item === 'object' && item !== null) {
                  // 객체의 모든 필드 검색
                  for (const itemKey in item) {
                    const itemValue = item[itemKey];
                    if (typeof itemValue === 'string' && itemValue.trim()) {
                      // 무기 피해 증폭
                      let weaponMatches = itemValue.match(/무기\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      if (!weaponMatches) {
                        weaponMatches = itemValue.match(/무기피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (!weaponMatches) {
                        weaponMatches = itemValue.match(/weapon\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (weaponMatches) {
                        weaponMatches.forEach(match => {
                          const numMatch = match.match(/(\d+\.?\d*)/);
                          if (numMatch) {
                            const ampValue = parseFloat(numMatch[1]) || 0;
                            if (ampValue > 0) {
                              nodeWeaponAmp += ampValue;
                              if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                            }
                          }
                        });
                      }
                      
                      // PVE 피해 증폭
                      let pveMatches = itemValue.match(/pve\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      if (!pveMatches) {
                        pveMatches = itemValue.match(/pve피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (!pveMatches) {
                        pveMatches = itemValue.match(/pve\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (pveMatches) {
                        pveMatches.forEach(match => {
                          const numMatch = match.match(/(\d+\.?\d*)/);
                          if (numMatch) {
                            const ampValue = parseFloat(numMatch[1]) || 0;
                            if (ampValue > 0) {
                              nodePveAmp += ampValue;
                              if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                            }
                          }
                        });
                      }
                      
                      // 치명타 피해 증폭
                      let criticalMatches = itemValue.match(/치명타\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      if (!criticalMatches) {
                        criticalMatches = itemValue.match(/치명타피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (!criticalMatches) {
                        criticalMatches = itemValue.match(/critical\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (criticalMatches) {
                        criticalMatches.forEach(match => {
                          const numMatch = match.match(/(\d+\.?\d*)/);
                          if (numMatch) {
                            const ampValue = parseFloat(numMatch[1]) || 0;
                            if (ampValue > 0) {
                              nodeCriticalAmp += ampValue;
                              if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                            }
                          }
                        });
                      }
                      
                      // 피해 증폭 (다른 키워드와 겹치지 않을 때만)
                      let damageMatches = itemValue.match(/피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      if (!damageMatches) {
                        damageMatches = itemValue.match(/피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (!damageMatches) {
                        damageMatches = itemValue.match(/damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                      }
                      if (damageMatches) {
                        damageMatches.forEach(match => {
                          const matchLower = match.toLowerCase();
                          if (!matchLower.includes('무기') && !matchLower.includes('pve') && !matchLower.includes('치명타') && !matchLower.includes('weapon') && !matchLower.includes('critical')) {
                            const numMatch = match.match(/(\d+\.?\d*)/);
                            if (numMatch) {
                              const ampValue = parseFloat(numMatch[1]) || 0;
                              if (ampValue > 0) {
                                nodeDamageAmp += ampValue;
                                if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                              }
                            }
                          }
                        });
                      }
                    }
                    
                    // value나 amount 필드가 숫자인 경우
                    if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                      const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                      if (weaponDamageAmpKeywords.some(keyword => itemName.includes(keyword.toLowerCase()))) {
                        nodeWeaponAmp += itemValue;
                        if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                      } else if (pveDamageAmpKeywords.some(keyword => itemName.includes(keyword.toLowerCase()))) {
                        nodePveAmp += itemValue;
                        if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                      } else if (criticalDamageAmpKeywords.some(keyword => itemName.includes(keyword.toLowerCase()))) {
                        nodeCriticalAmp += itemValue;
                        if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                      } else if (damageAmpKeywords.some(keyword => itemName.includes(keyword.toLowerCase()))) {
                        if (!itemName.includes('무기') && !itemName.includes('pve') && !itemName.includes('치명타') && !itemName.includes('weapon') && !itemName.includes('critical')) {
                          nodeDamageAmp += itemValue;
                          if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                        }
                      }
                    }
                  }
                }
              });
            }
            
            // 객체인 경우 재귀적으로 검색 - 4가지 피해 증폭을 각각 추적
            if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
              for (const subKey in value) {
                const subValue = value[subKey];
                if (typeof subValue === 'string' && subValue.trim()) {
                  // 무기 피해 증폭
                  let weaponMatches = subValue.match(/무기\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  if (!weaponMatches) {
                    weaponMatches = subValue.match(/무기피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (!weaponMatches) {
                    weaponMatches = subValue.match(/weapon\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (weaponMatches) {
                    weaponMatches.forEach(match => {
                      const numMatch = match.match(/(\d+\.?\d*)/);
                      if (numMatch) {
                        const ampValue = parseFloat(numMatch[1]) || 0;
                        if (ampValue > 0) {
                          nodeWeaponAmp += ampValue;
                          if (!foundInField) foundInField = `${key}.${subKey}`;
                        }
                      }
                    });
                  }
                  
                  // PVE 피해 증폭
                  let pveMatches = subValue.match(/pve\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  if (!pveMatches) {
                    pveMatches = subValue.match(/pve피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (!pveMatches) {
                    pveMatches = subValue.match(/pve\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (pveMatches) {
                    pveMatches.forEach(match => {
                      const numMatch = match.match(/(\d+\.?\d*)/);
                      if (numMatch) {
                        const ampValue = parseFloat(numMatch[1]) || 0;
                        if (ampValue > 0) {
                          nodePveAmp += ampValue;
                          if (!foundInField) foundInField = `${key}.${subKey}`;
                        }
                      }
                    });
                  }
                  
                  // 치명타 피해 증폭
                  let criticalMatches = subValue.match(/치명타\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  if (!criticalMatches) {
                    criticalMatches = subValue.match(/치명타피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (!criticalMatches) {
                    criticalMatches = subValue.match(/critical\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (criticalMatches) {
                    criticalMatches.forEach(match => {
                      const numMatch = match.match(/(\d+\.?\d*)/);
                      if (numMatch) {
                        const ampValue = parseFloat(numMatch[1]) || 0;
                        if (ampValue > 0) {
                          nodeCriticalAmp += ampValue;
                          if (!foundInField) foundInField = `${key}.${subKey}`;
                        }
                      }
                    });
                  }
                  
                  // 피해 증폭 (다른 키워드와 겹치지 않을 때만)
                  let damageMatches = subValue.match(/피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  if (!damageMatches) {
                    damageMatches = subValue.match(/피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (!damageMatches) {
                    damageMatches = subValue.match(/damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                  }
                  if (damageMatches) {
                    damageMatches.forEach(match => {
                      const matchLower = match.toLowerCase();
                      if (!matchLower.includes('무기') && !matchLower.includes('pve') && !matchLower.includes('치명타') && !matchLower.includes('weapon') && !matchLower.includes('critical')) {
                        const numMatch = match.match(/(\d+\.?\d*)/);
                        if (numMatch) {
                          const ampValue = parseFloat(numMatch[1]) || 0;
                          if (ampValue > 0) {
                            nodeDamageAmp += ampValue;
                            if (!foundInField) foundInField = `${key}.${subKey}`;
                          }
                        }
                      }
                    });
                  }
                }
              }
            }
          }
          
          // 노드별 합계를 보드 합계에 추가
          if (nodeWeaponAmp > 0) {
            boardWeaponAmp += nodeWeaponAmp;
          }
          if (nodePveAmp > 0) {
            boardPveAmp += nodePveAmp;
          }
          if (nodeDamageAmp > 0) {
            boardDamageAmp += nodeDamageAmp;
          }
          if (nodeCriticalAmp > 0) {
            boardCriticalAmp += nodeCriticalAmp;
          }
        });
        
        // 보드별 합계를 전체 합계에 추가 (보드별 필터링 적용)
        // 지켈(42): 무기 피해 증폭, PVE 피해 증폭, 피해 증폭
        if (isZikel) {
          if (boardWeaponAmp > 0) {
            weaponDaevanionPercent += boardWeaponAmp;
            weaponDamageAmpPercent += boardWeaponAmp;
          }
          if (boardPveAmp > 0) {
            pveDaevanionPercent += boardPveAmp;
            pveDamageAmpPercent += boardPveAmp;
          }
          if (boardDamageAmp > 0) {
            damageAmpDaevanionPercent += boardDamageAmp;
            damageAmpPercent += boardDamageAmp;
          }
        }
        
        // 바이젤(43): 치명타 피해 증폭만
        if (isVaizel) {
          if (boardCriticalAmp > 0) {
            criticalDaevanionPercent += boardCriticalAmp;
            criticalDamageAmpPercent += boardCriticalAmp;
          }
        }
        
        // 마르쿠탄(47): 무기 피해 증폭
        if (isMarkutan) {
          if (boardWeaponAmp > 0) {
            weaponDaevanionPercent += boardWeaponAmp;
            weaponDamageAmpPercent += boardWeaponAmp;
          }
        }
      }
    });
  }
  
  // 2-1. 데바니온 아리엘 보드에서 PVE 피해 증폭, 보스 피해 증폭 추출 (PVE 피해 증폭에만 추가)
  let daevanionArielPveAmp = 0;
  
  if (daevanionData && daevanionData[45]) {
    const boardData = daevanionData[45];
    let boardArielPveAmp = 0;
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node, nodeIndex) => {
        let nodeArielPveAmp = 0;
        let foundInField = null;
        
        // 노드의 모든 필드를 순회하며 검색
        for (const key in node) {
          const value = node[key];
          
          // 문자열인 경우 패턴 검색 (PVE 피해 증폭, 보스 피해 증폭)
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            
            // "PVE 피해 증폭 +X%", "보스 피해 증폭 +X%", "보스 피해 증가 +X%" 패턴 검색
            let matches = text.match(/(?:PVE\s*피해\s*증폭|보스\s*피해\s*증폭|보스\s*피해\s*증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            if (!matches) {
              matches = text.match(/(?:PVE피해증폭|보스피해증폭|보스피해증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            if (!matches) {
              matches = text.match(/(?:pve\s*damage\s*amplification|boss\s*damage\s*amplification|boss\s*damage\s*increase)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  const ampValue = parseFloat(numMatch[1]) || 0;
                  if (ampValue > 0) {
                    nodeArielPveAmp += ampValue;
                    if (!foundInField) foundInField = key;
                  }
                }
              });
            }
          }
          
          // 배열인 경우
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item, itemIndex) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/(?:PVE\s*피해\s*증폭|보스\s*피해\s*증폭|보스\s*피해\s*증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    if (!matches) {
                      matches = itemValue.match(/(?:PVE피해증폭|보스피해증폭|보스피해증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    if (!matches) {
                      matches = itemValue.match(/(?:pve\s*damage\s*amplification|boss\s*damage\s*amplification|boss\s*damage\s*increase)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+\.?\d*)/);
                        if (numMatch) {
                          const ampValue = parseFloat(numMatch[1]) || 0;
                          if (ampValue > 0) {
                            nodeArielPveAmp += ampValue;
                            if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                          }
                        }
                      });
                    }
                  }
                  
                  // value나 amount 필드가 숫자인 경우
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if (itemName.includes('pve 피해 증폭') || itemName.includes('보스 피해 증폭') || itemName.includes('보스 피해 증가') || 
                        itemName.includes('pve damage amplification') || itemName.includes('boss damage amplification') || itemName.includes('boss damage increase')) {
                      nodeArielPveAmp += itemValue;
                      if (!foundInField) foundInField = `${key}[${itemIndex}].${itemKey}`;
                    }
                  }
                }
              }
            });
          }
          
          // 객체인 경우 재귀적으로 검색
          if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
            for (const subKey in value) {
              const subValue = value[subKey];
              if (typeof subValue === 'string' && subValue.trim()) {
                let matches = subValue.match(/(?:PVE\s*피해\s*증폭|보스\s*피해\s*증폭|보스\s*피해\s*증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                if (!matches) {
                  matches = subValue.match(/(?:PVE피해증폭|보스피해증폭|보스피해증가)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                if (!matches) {
                  matches = subValue.match(/(?:pve\s*damage\s*amplification|boss\s*damage\s*amplification|boss\s*damage\s*increase)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                }
                
                if (matches) {
                  matches.forEach(match => {
                    const numMatch = match.match(/(\d+\.?\d*)/);
                    if (numMatch) {
                      const ampValue = parseFloat(numMatch[1]) || 0;
                      if (ampValue > 0) {
                        nodeArielPveAmp += ampValue;
                        if (!foundInField) foundInField = `${key}.${subKey}`;
                      }
                    }
                  });
                }
              }
            }
          }
        }
        
        if (nodeArielPveAmp > 0) {
          boardArielPveAmp += nodeArielPveAmp;
        }
      });
      
      if (boardArielPveAmp > 0) {
        daevanionArielPveAmp = boardArielPveAmp;
      }
    }
  }
  
  if (daevanionArielPveAmp > 0) {
    pveDaevanionArielPercent = daevanionArielPveAmp;
    pveDamageAmpPercent += daevanionArielPveAmp;
  } else if (daevanionData && daevanionData[45]) {
    pveDaevanionArielPercent = 0;
  }
  
  // 3. 타이틀 장착 효과에서 피해 증폭 추출 (PVP 피해 증폭 제외, 4가지 피해 증폭을 각각 추적)
  if (titles && Array.isArray(titles)) {
    titles.forEach((title) => {
      const titleName = title.name || '알 수 없음';
      
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          const effectText = String(effect || '').toLowerCase();
          
          // PVP 피해 증폭은 제외 (PVE 전투 점수 계산이므로)
          if (effectText.includes('pvp 피해 증폭') || effectText.includes('pvp피해증폭') || effectText.includes('pvp damage amplification')) {
            return; // PVP 피해 증폭은 건너뛰기
          }
          
          // 무기 피해 증폭
          let weaponMatches = effectText.match(/무기\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!weaponMatches) {
            weaponMatches = effectText.match(/무기피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (!weaponMatches) {
            weaponMatches = effectText.match(/weapon\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (weaponMatches) {
            weaponMatches.forEach(match => {
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const ampValue = parseFloat(numMatch[1]) || 0;
                if (ampValue > 0) {
                  weaponTitlePercent += ampValue;
                  weaponDamageAmpPercent += ampValue;
                }
              }
            });
          }
          
          // PVE 피해 증폭
          let pveMatches = effectText.match(/pve\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!pveMatches) {
            pveMatches = effectText.match(/pve피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (!pveMatches) {
            pveMatches = effectText.match(/pve\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (pveMatches) {
            pveMatches.forEach(match => {
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const ampValue = parseFloat(numMatch[1]) || 0;
                if (ampValue > 0) {
                  pveTitlePercent += ampValue;
                  pveDamageAmpPercent += ampValue;
                }
              }
            });
          }
          
          // 치명타 피해 증폭
          let criticalMatches = effectText.match(/치명타\s*피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!criticalMatches) {
            criticalMatches = effectText.match(/치명타피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (!criticalMatches) {
            criticalMatches = effectText.match(/critical\s*damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          if (criticalMatches) {
            criticalMatches.forEach(match => {
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const ampValue = parseFloat(numMatch[1]) || 0;
                if (ampValue > 0) {
                  criticalTitlePercent += ampValue;
                  criticalDamageAmpPercent += ampValue;
                }
              }
            });
          }
          
          // 피해 증폭 (다른 키워드와 겹치지 않을 때만)
          // 원본 effectText에서 먼저 치명타, 무기, PVE 키워드 확인
          if (!effectText.includes('무기') && !effectText.includes('pve') && !effectText.includes('치명타') && 
              !effectText.includes('weapon') && !effectText.includes('critical')) {
            let damageMatches = effectText.match(/피해\s*증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            if (!damageMatches) {
              damageMatches = effectText.match(/피해증폭\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            if (!damageMatches) {
              damageMatches = effectText.match(/damage\s*amplification\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            if (damageMatches) {
              damageMatches.forEach(match => {
                const numMatch = match.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  const ampValue = parseFloat(numMatch[1]) || 0;
                  if (ampValue > 0) {
                    damageAmpTitlePercent += ampValue;
                    damageAmpPercent += ampValue;
                  }
                }
              });
            }
          }
        });
      }
    });
  }
  
  // 정수 합계를 퍼센트로 변환 (정수 10당 0.1%)
  const weaponIntegerToPercent = weaponDamageAmpInteger / 10 * 0.1;
  const pveIntegerToPercent = pveDamageAmpInteger / 10 * 0.1;
  const damageAmpIntegerToPercent = damageAmpInteger / 10 * 0.1;
  const criticalIntegerToPercent = criticalDamageAmpInteger / 10 * 0.1;
  
  // 날개 장착 효과로 인한 피해 증폭 추가
  let wingDamageAmpPercent = 0;
  const wingEffects = getWingEffects();
  if (wingEffects && wingEffects.damageAmplification) {
    wingDamageAmpPercent = wingEffects.damageAmplification;
  }
  
  // 아르카나 세트 효과 적용
  const arcanaSetCountsForDmg = window.arcanaSetCounts || { purity: 0, frenzy: 0 };
  if (arcanaSetCountsForDmg.purity >= 4) {
    arcanaPurityCriticalDamageAmpPercent = 5.0; // 순수 4세트: 치명타 피해 증폭 5%
  }
  if (arcanaSetCountsForDmg.frenzy >= 4) {
    arcanaFrenzyDamageAmpPercent = 5.0; // 광분 4세트: 보스 피해 증폭 5%
  }
  
  // 최종 피해 증폭 퍼센트 = 기존 퍼센트 + 정수 변환 퍼센트
  const finalWeaponDamageAmpPercent = weaponDamageAmpPercent + weaponIntegerToPercent;
  const finalPveDamageAmpPercent = pveDamageAmpPercent + pveIntegerToPercent;
  const finalDamageAmpPercent = damageAmpPercent + damageAmpIntegerToPercent + wingDamageAmpPercent + pvpGearDebuffPercent + arcanaFrenzyDamageAmpPercent; // 날개 효과 포함 + PVP 장비 디버프 + 아르카나 광분 4세트
  const finalCriticalDamageAmpPercent = criticalDamageAmpPercent + criticalIntegerToPercent + arcanaPurityCriticalDamageAmpPercent; // 아르카나 순수 4세트 포함
  
  // 총합 (DPS 계산용)
  const totalFinalDamageAmpPercent = finalWeaponDamageAmpPercent + finalPveDamageAmpPercent + finalDamageAmpPercent + finalCriticalDamageAmpPercent;
  
  const result = {
    // 4가지 피해 증폭 각각
    weaponDamageAmp: {
      totalPercent: finalWeaponDamageAmpPercent,
      totalInteger: weaponDamageAmpInteger,
      breakdown: {
        soulPercent: weaponSoulPercent,
        equipmentBasePercent: weaponEquipmentBasePercent,
        stoneInteger: weaponStoneInteger,
        stonePercent: weaponIntegerToPercent,
        daevanionPercent: weaponDaevanionPercent,
        daevanionArielPercent: 0, // 아리엘은 PVE만
        titlePercent: weaponTitlePercent
      }
    },
    pveDamageAmp: {
      totalPercent: finalPveDamageAmpPercent,
      totalInteger: pveDamageAmpInteger,
      breakdown: {
        soulPercent: pveSoulPercent,
        equipmentBasePercent: pveEquipmentBasePercent,
        stoneInteger: pveStoneInteger,
        stonePercent: pveIntegerToPercent,
        daevanionPercent: pveDaevanionPercent,
        daevanionArielPercent: pveDaevanionArielPercent,
        titlePercent: pveTitlePercent
      }
    },
    damageAmp: {
      totalPercent: finalDamageAmpPercent,
      totalInteger: damageAmpInteger,
      breakdown: {
        soulPercent: damageAmpSoulPercent,
        equipmentBasePercent: damageAmpEquipmentBasePercent,
        stoneInteger: damageAmpStoneInteger,
        stonePercent: damageAmpIntegerToPercent,
        daevanionPercent: damageAmpDaevanionPercent,
        daevanionArielPercent: 0, // 아리엘은 PVE만
        titlePercent: damageAmpTitlePercent,
        wingPercent: wingDamageAmpPercent,
        wingName: wingEffects ? wingEffects.name : null,
        pvpGearDebuffPercent: pvpGearDebuffPercent,
        pvpGearCounts: pvpGearCounts,
        pvpGearDebuffByRank: pvpGearDebuffByRank,
        arcanaFrenzyPercent: arcanaFrenzyDamageAmpPercent // 광분 4세트: 보스 피해 증폭 5%
      }
    },
    criticalDamageAmp: {
      totalPercent: finalCriticalDamageAmpPercent,
      totalInteger: criticalDamageAmpInteger,
      breakdown: {
        soulPercent: criticalSoulPercent,
        equipmentBasePercent: criticalEquipmentBasePercent,
        stoneInteger: criticalStoneInteger,
        stonePercent: criticalIntegerToPercent,
        daevanionPercent: criticalDaevanionPercent,
        daevanionArielPercent: 0, // 아리엘은 PVE만
        titlePercent: criticalTitlePercent,
        arcanaPurityPercent: arcanaPurityCriticalDamageAmpPercent // 순수 4세트: 치명타 피해 증폭 5%
      }
    },
    // DPS 계산용 총합 (하위 호환성)
    totalDamageAmpPercent: totalFinalDamageAmpPercent,
    finalDamageAmpPercent: totalFinalDamageAmpPercent,
    pvpGearDebuff: {
      totalPercent: pvpGearDebuffPercent,
      counts: pvpGearCounts,
      byRank: pvpGearDebuffByRank
    }
  };
  
  // 전역 변수에 저장
  window.damageAmplificationResult = result;
  
  // 피해 증폭 표시 업데이트
  displayDamageAmplificationStats(result);
  
  return result;
}

function calculateCombatSpeed(equipment, accessories, statData, daevanionData, titles) {
  let totalCombatSpeed = 0;
  let soulCombatSpeed = 0;
  let accessoryCombatSpeed = 0; // 장신구 기본 옵션 전투 속도
  let timeCombatSpeed = 0;
  let daevanionCombatSpeed = 0;
  
  const allItems = [...(equipment || []), ...(accessories || [])];
  allItems.forEach((item) => {
    const itemName = item.name || '알 수 없음';
    
    // 영혼 각인에서 전투 속도 추출 (sub_stats)
    if (item.sub_stats) {
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach((stat) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseFloat(stat.value || stat.amount || 0);
            
            if ((name.includes('전투 속도') || name.includes('전투속도') || name.includes('combat speed') || name.includes('combatspeed')) && value > 0) {
              soulCombatSpeed += value;
              totalCombatSpeed += value;
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseFloat(item.sub_stats[key] || 0);
          
          if ((keyLower.includes('전투 속도') || keyLower.includes('전투속도') || keyLower.includes('combat speed') || keyLower.includes('combatspeed')) && value > 0) {
            soulCombatSpeed += value;
            totalCombatSpeed += value;
          }
        }
      }
    }
  });
  
  // 장신구 기본 옵션에서 전투 속도 추출 (main_stats)
  if (Array.isArray(accessories) && accessories.length > 0) {
    accessories.forEach((accessory) => {
      const accessoryName = accessory.name || '알 수 없음';
      
      if (accessory.main_stats) {
        if (Array.isArray(accessory.main_stats)) {
          accessory.main_stats.forEach((stat) => {
            if (typeof stat === 'object' && stat !== null) {
              const name = (stat.name || stat.type || '').toLowerCase();
              const value = parseFloat(stat.value || stat.minValue || 0);
              
              if ((name.includes('전투 속도') || name.includes('전투속도') || name.includes('combat speed') || name.includes('combatspeed')) && value > 0) {
                accessoryCombatSpeed += value;
                totalCombatSpeed += value;
              }
            }
          });
        } else if (typeof accessory.main_stats === 'object') {
          for (const key of Object.keys(accessory.main_stats)) {
            const keyLower = key.toLowerCase();
            const value = parseFloat(accessory.main_stats[key] || 0);
            
            if ((keyLower.includes('전투 속도') || keyLower.includes('전투속도') || keyLower.includes('combat speed') || keyLower.includes('combatspeed')) && value > 0) {
              accessoryCombatSpeed += value;
              totalCombatSpeed += value;
            }
          }
        }
      }
    });
  }
  
  if (statData && statData.statList) {
    const timeStat = statData.statList.find(stat => 
      stat.type === 'Time' || (stat.name && (stat.name.includes('시간') || stat.name.includes('Time') || stat.name.includes('시엘')))
    );
    
    if (timeStat) {
      // 시간 스탯의 값 찾기 (스탯 1당 0.2%, 2배 적용)
      const timeValue = parseInt(timeStat.value || 0);
      if (timeValue > 0) {
        timeCombatSpeed = timeValue * 0.2; // 스탯 1당 0.2%
        totalCombatSpeed += timeCombatSpeed;
      }
    }
  }
  
  if (daevanionData && daevanionData[41]) {
    const boardData = daevanionData[41];
    let boardCombatSpeed = 0;
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node) => {
        let nodeCombatSpeed = 0;
        
        for (const key in node) {
          const value = node[key];
          
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            let matches = text.match(/전투\s*속도\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            if (!matches) {
              matches = text.match(/전투속도\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            if (!matches) {
              matches = text.match(/combat\s*speed\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  const speedValue = parseFloat(numMatch[1]) || 0;
                  if (speedValue > 0) {
                    nodeCombatSpeed += speedValue;
                  }
                }
              });
            }
          }
          
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/전투\s*속도\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    if (!matches) {
                      matches = itemValue.match(/전투속도\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    if (!matches) {
                      matches = itemValue.match(/combat\s*speed\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+\.?\d*)/);
                        if (numMatch) {
                          const speedValue = parseFloat(numMatch[1]) || 0;
                          if (speedValue > 0) {
                            nodeCombatSpeed += speedValue;
                          }
                        }
                      });
                    }
                  }
                  
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if (itemName.includes('전투 속도') || itemName.includes('전투속도') || itemName.includes('combat speed')) {
                      nodeCombatSpeed += itemValue;
                    }
                  }
                }
              }
            });
          }
        }
        
        if (nodeCombatSpeed > 0) {
          boardCombatSpeed += nodeCombatSpeed;
        }
      });
      
      if (boardCombatSpeed > 0) {
        daevanionCombatSpeed = boardCombatSpeed;
        totalCombatSpeed += boardCombatSpeed;
      }
    }
  }
  
  // 타이틀에서 전투 속도 추출
  let titleCombatSpeed = 0;
  if (titles && Array.isArray(titles) && titles.length > 0) {
    titles.forEach((title) => {
      if (title && title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          if (typeof effect === 'string') {
            // "전투 속도 +3.5%" 형식 찾기
            const match = effect.match(/전투\s*속도\s*[+＋]\s*(\d+\.?\d*)\s*%/i);
            if (match) {
              const speedValue = parseFloat(match[1]) || 0;
              if (speedValue > 0) {
                titleCombatSpeed += speedValue;
                totalCombatSpeed += speedValue;
              }
            }
          }
        });
      }
    });
  }
  
  const result = {
    totalCombatSpeed: totalCombatSpeed,
    breakdown: {
      soulCombatSpeed: soulCombatSpeed,
      accessoryCombatSpeed: accessoryCombatSpeed,
      timeCombatSpeed: timeCombatSpeed,
      daevanionCombatSpeed: daevanionCombatSpeed,
      titleCombatSpeed: titleCombatSpeed
    }
  };
  
  window.combatSpeedResult = result;
  displayCombatSpeedStats(result);
  
  return result;
}

function calculateCooldownReduction(statData, daevanionData, titles) {
  let totalCooldownReduction = 0;
  let titleCooldownReduction = 0;
  let illusionCooldownReduction = 0;
  let daevanionCooldownReduction = 0;
  
  // 1. 타이틀 장착 효과에서 재사용 시간 감소 추출
  if (titles && Array.isArray(titles)) {
    titles.forEach((title) => {
      const titleName = title.name || '알 수 없음';
      
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          const effectText = String(effect || '');
          
          // 재사용 시간 감소 패턴 매칭
          let matches = effectText.match(/(?:재사용\s*시간\s*감소|재사용시간감소|재사용\s*대기\s*시간\s*감소|재사용대기시간감소|재시전\s*시간\s*감소|재시전시간감소|재시전\s*대기\s*시간\s*감소|재시전대기시간감소)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!matches) {
            matches = effectText.match(/cooldown\s*reduction\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          
          if (matches) {
            matches.forEach(match => {
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const reductionValue = parseFloat(numMatch[1]) || 0;
                if (reductionValue > 0) {
                  titleCooldownReduction += reductionValue;
                  totalCooldownReduction += reductionValue;
                }
              }
            });
          }
        });
      }
    });
  }
  
  // 2. 주신 스탯 - 환상[카이시넬]에서 재사용 시간 감소 추출
  // 스탯 값 × 0.1% × 2배 = 스탯 값 × 0.2%
  if (statData && statData.statList) {
    const illusionStat = statData.statList.find(stat => 
      stat.type === 'Illusion' || (stat.name && (stat.name.includes('환상') || stat.name.includes('Illusion') || stat.name.includes('카이시넬') || stat.name.includes('Kaisinel')))
    );
    
    if (illusionStat) {
      const illusionValue = parseInt(illusionStat.value || 0);
      if (illusionValue > 0) {
        // 스탯 1당 0.2% (0.1% × 2배)
        illusionCooldownReduction = illusionValue * 0.2;
        totalCooldownReduction += illusionCooldownReduction;
      }
    }
  }
  
  // 3. 데바니온 네자칸(41) 보드에서 재사용 시간 감소 추출
  if (daevanionData && daevanionData[41]) {
    const boardData = daevanionData[41];
    let boardCooldownReduction = 0;
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node) => {
        let nodeCooldownReduction = 0;
        
        for (const key in node) {
          const value = node[key];
          
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            let matches = text.match(/(?:재사용\s*시간\s*감소|재사용시간감소|재사용\s*대기\s*시간\s*감소|재사용대기시간감소|재시전\s*시간\s*감소|재시전시간감소|재시전\s*대기\s*시간\s*감소|재시전대기시간감소)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            if (!matches) {
              matches = text.match(/cooldown\s*reduction\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  const speedValue = parseFloat(numMatch[1]) || 0;
                  if (speedValue > 0) {
                    nodeCooldownReduction += speedValue;
                  }
                }
              });
            }
          }
          
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/(?:재사용\s*시간\s*감소|재사용시간감소|재사용\s*대기\s*시간\s*감소|재사용대기시간감소|재시전\s*시간\s*감소|재시전시간감소|재시전\s*대기\s*시간\s*감소|재시전대기시간감소)\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    if (!matches) {
                      matches = itemValue.match(/cooldown\s*reduction\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+\.?\d*)/);
                        if (numMatch) {
                          const speedValue = parseFloat(numMatch[1]) || 0;
                          if (speedValue > 0) {
                            nodeCooldownReduction += speedValue;
                          }
                        }
                      });
                    }
                  }
                }
              }
            });
          }
        }
        
        if (nodeCooldownReduction > 0) {
          boardCooldownReduction += nodeCooldownReduction;
        }
      });
      
      if (boardCooldownReduction > 0) {
        daevanionCooldownReduction = boardCooldownReduction;
        totalCooldownReduction += daevanionCooldownReduction;
      }
    }
  }
  
  // 날개 장착 효과로 인한 재사용 시간 감소 추가
  let wingCooldownReduction = 0;
  const wingEffects = getWingEffects();
  if (wingEffects && wingEffects.cooldownReduction) {
    wingCooldownReduction = wingEffects.cooldownReduction;
    totalCooldownReduction += wingCooldownReduction;
  }
  
  const result = {
    totalCooldownReduction: totalCooldownReduction,
    breakdown: {
      titleCooldownReduction: titleCooldownReduction,
      illusionCooldownReduction: illusionCooldownReduction,
      daevanionCooldownReduction: daevanionCooldownReduction,
      wingCooldownReduction: wingCooldownReduction,
      wingName: wingEffects ? wingEffects.name : null
    }
  };
  
  window.cooldownReductionResult = result;
  displayCooldownReductionStats(result);
  
  return result;
}

function calculateStunHit(equipment, accessories, statData, titles) {
  let totalStunHitPercent = 0;
  let titleStunHitPercent = 0;
  let wisdomStunHitPercent = 0;
  let soulStunHitPercent = 0;
  let wingStunHitPercent = 0;
  
  // 1. 타이틀 장착 효과에서 강타 추출
  if (titles && Array.isArray(titles)) {
    titles.forEach((title) => {
      const titleName = title.name || '알 수 없음';
      
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          const effectText = String(effect || '');
          
          // 강타 패턴 매칭
          let matches = effectText.match(/강타\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!matches) {
            matches = effectText.match(/stun\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          
          if (matches) {
            matches.forEach(match => {
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const stunValue = parseFloat(numMatch[1]) || 0;
                if (stunValue > 0) {
                  titleStunHitPercent += stunValue;
                  totalStunHitPercent += stunValue;
                }
              }
            });
          }
        });
      }
    });
  }
  
  // 2. 주신 스탯 중 지혜[루미엘] 스탯에서 강타 추출
  if (statData && statData.statList) {
    const wisdomStat = statData.statList.find(stat => 
      stat.type === 'Wisdom' || (stat.name && stat.name.includes('지혜'))
    );
    
    if (wisdomStat && wisdomStat.statSecondList && Array.isArray(wisdomStat.statSecondList)) {
      const wisdomValue = parseInt(wisdomStat.value || 0);
      
      // statSecondList에서 "강타" 효과 찾기
      wisdomStat.statSecondList.forEach((effect, index) => {
        const effectText = String(effect || '');
        if (effectText.includes('강타') || effectText.includes('stun')) {
          // 지혜[루미엘]: 정신력 소모 감소(-), 강타 (각각 2배, 첫 번째는 음수)
          // 강타는 두 번째 효과이므로 index 1
          if (index === 1) {
            // 스탯 값 × 0.1% × 2배
            wisdomStunHitPercent = wisdomValue * 0.1 * 2;
            totalStunHitPercent += wisdomStunHitPercent;
          }
        }
      });
    }
  }
  
  // 3. 장비 영혼 각인에서 강타 추출
  const allItems = [...(equipment || []), ...(accessories || [])];
  allItems.forEach((item) => {
    if (!item || !item.sub_stats) return;
    if (Array.isArray(item.sub_stats)) {
      item.sub_stats.forEach((stat) => {
        if (typeof stat === 'object') {
          const name = (stat.name || stat.type || '').trim().toLowerCase();
          const value = parseFloat(stat.value || stat.amount || 0);
          if (value > 0) {
            const isExactStun = name === '강타' || name === 'stun';
            if (isExactStun) {
              soulStunHitPercent += value;
              totalStunHitPercent += value;
            }
          }
        }
      });
    } else if (typeof item.sub_stats === 'object') {
      for (const key of Object.keys(item.sub_stats)) {
        const keyLower = key.trim().toLowerCase();
        const value = parseFloat(item.sub_stats[key] || 0);
        if (value > 0) {
          const isExactStun = keyLower === '강타' || keyLower === 'stun';
          if (isExactStun) {
            soulStunHitPercent += value;
            totalStunHitPercent += value;
          }
        }
      }
    }
  });
  
  // 4. 날개 장착 효과에서 강타 추출
  const wingEffects = getWingEffects();
  if (wingEffects && wingEffects.stunHit) {
    wingStunHitPercent = wingEffects.stunHit;
    totalStunHitPercent += wingStunHitPercent;
  }
  
  const result = {
    totalStunHitPercent: totalStunHitPercent,
    breakdown: {
      titleStunHitPercent: titleStunHitPercent,
      wisdomStunHitPercent: wisdomStunHitPercent,
      soulStunHitPercent: soulStunHitPercent,
      wingStunHitPercent: wingStunHitPercent,
      wingName: wingEffects ? wingEffects.name : null
    }
  };
  
  // 전역 변수에 저장
  window.stunHitResult = result;
  
  // 강타 표시 업데이트
  displayStunHitStats(result);
  
  return result;
}

function calculateMultiHit(equipment, accessories, daevanionData) {
  let totalMultiHitPercent = 0;
  let soulMultiHitPercent = 0;
  let baseMultiHitPercent = 0;
  let daevanionMultiHitPercent = 0;
  
  // 모든 아이템을 하나의 배열로 합치기
  const allItems = [...(equipment || []), ...(accessories || [])];
  
  allItems.forEach((item) => {
    if (!item) return;
    const itemName = item.name || '알 수 없음';
    
    // 1. 영혼 각인에서 다단 히트 적중 추출 (sub_stats)
    if (item.sub_stats) {
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach((stat) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseFloat(stat.value || stat.amount || 0);
            
            // 다단 히트 적중 패턴 매칭
            if (value > 0 && 
                (name.includes('다단히트') || name.includes('다단 히트') || 
                 (name.includes('multi') && name.includes('hit')))) {
              soulMultiHitPercent += value;
              totalMultiHitPercent += value;
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseFloat(item.sub_stats[key] || 0);
          
          // 다단 히트 적중 패턴 매칭
          if (value > 0 && 
              (keyLower.includes('다단히트') || keyLower.includes('다단 히트') || 
               (keyLower.includes('multi') && keyLower.includes('hit')))) {
            soulMultiHitPercent += value;
            totalMultiHitPercent += value;
          }
        }
      }
    }
    
    // 2. 기본 옵션에서 다단 히트 적중 추출 (main_stats)
    if (item.main_stats) {
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach((stat) => {
          if (typeof stat === 'object' && stat !== null) {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseFloat(stat.value || stat.minValue || 0);
            
            // 다단 히트 적중 패턴 매칭
            if (value > 0 && 
                (name.includes('다단히트') || name.includes('다단 히트') || 
                 (name.includes('multi') && name.includes('hit')))) {
              baseMultiHitPercent += value;
              totalMultiHitPercent += value;
            }
          }
        });
      } else if (typeof item.main_stats === 'object') {
        for (const key of Object.keys(item.main_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseFloat(item.main_stats[key] || 0);
          
          // 다단 히트 적중 패턴 매칭
          if (value > 0 && 
              (keyLower.includes('다단히트') || keyLower.includes('다단 히트') || 
               (keyLower.includes('multi') && keyLower.includes('hit')))) {
            baseMultiHitPercent += value;
            totalMultiHitPercent += value;
          }
        }
      }
    }
  });
  
  // 3. 데바니온 - 트리니엘 보드에서 다단 히트 적중 추출
  if (daevanionData && daevanionData[44]) {
    const boardData = daevanionData[44];
    let boardMultiHit = 0;
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node) => {
        let nodeMultiHit = 0;
        
        for (const key in node) {
          const value = node[key];
          
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            let matches = text.match(/다단\s*히트\s*적중\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            if (!matches) {
              matches = text.match(/다단히트\s*적중\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            if (!matches) {
              matches = text.match(/multi.*hit.*accuracy\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+\.?\d*)/);
                if (numMatch) {
                  const multiHitValue = parseFloat(numMatch[1]) || 0;
                  if (multiHitValue > 0) {
                    nodeMultiHit += multiHitValue;
                  }
                }
              });
            }
          }
          
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/다단\s*히트\s*적중\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    if (!matches) {
                      matches = itemValue.match(/다단히트\s*적중\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    if (!matches) {
                      matches = itemValue.match(/multi.*hit.*accuracy\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+\.?\d*)/);
                        if (numMatch) {
                          const multiHitValue = parseFloat(numMatch[1]) || 0;
                          if (multiHitValue > 0) {
                            nodeMultiHit += multiHitValue;
                          }
                        }
                      });
                    }
                  }
                  
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if (itemName.includes('다단히트') || itemName.includes('다단 히트') || 
                        (itemName.includes('multi') && itemName.includes('hit'))) {
                      nodeMultiHit += itemValue;
                    }
                  }
                }
              }
            });
          }
        }
        
        if (nodeMultiHit > 0) {
          boardMultiHit += nodeMultiHit;
        }
      });
      
      if (boardMultiHit > 0) {
        daevanionMultiHitPercent = boardMultiHit;
        totalMultiHitPercent += boardMultiHit;
      }
    }
  }
  
  const result = {
    totalMultiHitPercent: totalMultiHitPercent,
    breakdown: {
      soulMultiHitPercent: soulMultiHitPercent,
      baseMultiHitPercent: baseMultiHitPercent,
      daevanionMultiHitPercent: daevanionMultiHitPercent
    }
  };
  
  // 전역 변수에 저장
  window.multiHitResult = result;
  
  // 다단 히트 적중 표시 업데이트
  displayMultiHitStats(result);
  
  return result;
}

function calculatePerfect(equipment, accessories, statData, titles) {
  let totalPerfectPercent = 0;
  let titlePerfectPercent = 0;
  let justicePerfectPercent = 0;
  let accessoryPerfectPercent = 0;
  let soulPerfectPercent = 0;
  
  // 1. 타이틀 장착 효과에서 완벽 추출
  if (titles && Array.isArray(titles)) {
    titles.forEach((title) => {
      const titleName = title.name || '알 수 없음';
      
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          const effectText = String(effect || '');
          
          // 완벽 패턴 매칭 (완벽 저항 제외)
          let matches = effectText.match(/완벽\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          if (!matches) {
            matches = effectText.match(/perfect\s*[+＋]\s*(\d+\.?\d*)\s*%/gi);
          }
          
          if (matches) {
            matches.forEach(match => {
              // 완벽 저항 제외
              const matchLower = match.toLowerCase();
              if (matchLower.includes('완벽 저항') || matchLower.includes('perfect resistance')) {
                return; // 이 매칭은 건너뜀
              }
              
              const numMatch = match.match(/(\d+\.?\d*)/);
              if (numMatch) {
                const perfectValue = parseFloat(numMatch[1]) || 0;
                if (perfectValue > 0) {
                  titlePerfectPercent += perfectValue;
                  totalPerfectPercent += perfectValue;
                }
              }
            });
          }
        });
      }
    });
  }
  
  // 2. 주신 스탯 - 정의[네자칸]에서 완벽 추출
  if (statData && statData.statList) {
    const justiceStat = statData.statList.find(stat => 
      stat.type === 'Justice' || (stat.name && stat.name.includes('정의'))
    );
    
    if (justiceStat && justiceStat.statSecondList && Array.isArray(justiceStat.statSecondList)) {
      const justiceValue = parseInt(justiceStat.value || 0);
      
      // statSecondList에서 "완벽" 효과 찾기
      justiceStat.statSecondList.forEach((effect, index) => {
        const effectText = String(effect || '');
        if (effectText.includes('완벽') || effectText.includes('perfect')) {
          // 정의[네자칸]: 방어력 증가, 완벽 (각각 2배)
          // 완벽은 두 번째 효과이므로 index 1
          if (index === 1) {
            // 스탯 값 × 0.1% × 2배
            justicePerfectPercent = justiceValue * 0.1 * 2;
            totalPerfectPercent += justicePerfectPercent;
          }
        }
      });
    }
  }
  
  // 3. 장신구 기본 옵션에서 완벽 추출
  if (Array.isArray(accessories) && accessories.length > 0) {
    accessories.forEach((accessory) => {
      const accessoryName = accessory.name || '알 수 없음';
      
      if (accessory.main_stats) {
        if (Array.isArray(accessory.main_stats)) {
          accessory.main_stats.forEach((stat) => {
            if (typeof stat === 'object' && stat !== null) {
              const name = (stat.name || stat.type || '').toLowerCase();
              const value = parseFloat(stat.value || stat.minValue || 0);
              
              // 정확히 "완벽"만 매칭 (완벽 저항 제외)
              if (value > 0 && 
                  ((name === '완벽' || name === 'perfect') || 
                   (name.startsWith('완벽 ') && !name.includes('완벽 저항')) ||
                   (name.startsWith('perfect ') && !name.includes('perfect resistance')))) {
                accessoryPerfectPercent += value;
                totalPerfectPercent += value;
              }
            }
          });
        } else if (typeof accessory.main_stats === 'object') {
          for (const key of Object.keys(accessory.main_stats)) {
            const keyLower = key.toLowerCase();
            const value = parseFloat(accessory.main_stats[key] || 0);
            
            // 정확히 "완벽"만 매칭 (완벽 저항 제외)
            if (value > 0 && 
                ((keyLower === '완벽' || keyLower === 'perfect') || 
                 (keyLower.startsWith('완벽 ') && !keyLower.includes('완벽 저항')) ||
                 (keyLower.startsWith('perfect ') && !keyLower.includes('perfect resistance')))) {
              accessoryPerfectPercent += value;
              totalPerfectPercent += value;
            }
          }
        }
      }
    });
  }
  
  // 4. 장비 영혼 각인에서 완벽 추출
  const allItems = [...(equipment || []), ...(accessories || [])];
  allItems.forEach((item) => {
    if (!item || !item.sub_stats) return;
    if (Array.isArray(item.sub_stats)) {
      item.sub_stats.forEach((stat) => {
        if (typeof stat === 'object') {
          const name = (stat.name || stat.type || '').trim().toLowerCase();
          const value = parseFloat(stat.value || stat.amount || 0);
          if (value > 0) {
            const isExactPerfect = name === '완벽' || name === 'perfect';
            if (isExactPerfect) {
              soulPerfectPercent += value;
              totalPerfectPercent += value;
            }
          }
        }
      });
    } else if (typeof item.sub_stats === 'object') {
      for (const key of Object.keys(item.sub_stats)) {
        const keyLower = key.trim().toLowerCase();
        const value = parseFloat(item.sub_stats[key] || 0);
        if (value > 0) {
          const isExactPerfect = keyLower === '완벽' || keyLower === 'perfect';
          if (isExactPerfect) {
            soulPerfectPercent += value;
            totalPerfectPercent += value;
          }
        }
      }
    }
  });
  
  const result = {
    totalPerfectPercent: totalPerfectPercent,
    breakdown: {
      titlePerfectPercent: titlePerfectPercent,
      justicePerfectPercent: justicePerfectPercent,
      accessoryPerfectPercent: accessoryPerfectPercent,
      soulPerfectPercent: soulPerfectPercent
    }
  };
  
  // 전역 변수에 저장
  window.perfectResult = result;
  
  // 완벽 표시 업데이트
  displayPerfectStats(result);
  
  return result;
}

function calculateAccuracy(equipment, accessories, statData, daevanionData) {
  let totalIntegerAccuracyMin = 0; // 정수 명중 최소값 합계
  let totalIntegerAccuracyMax = 0; // 정수 명중 최대값 합계
  let totalPercentAccuracy = 0; // 퍼센트 명중 합계
  
  // Breakdown 정보 추적
  let baseAccuracyMin = 0; // 무기/가더 기본 옵션 명중 (최소)
  let baseAccuracyMax = 0; // 무기/가더 기본 옵션 명중 (최대)
  let stoneAccuracy = 0; // 마석 각인 추가 명중
  let soulAccuracy = 0; // 영혼 각인 명중
  let titleEquipAdditionalAccuracy = 0; // 타이틀 장착 효과 - 추가 명중 (퍼센트 보너스 적용)
  let titleEquipPveAccuracy = 0; // 타이틀 장착 효과 - PVE 명중 (퍼센트 보너스 미적용)
  let titleAccuracyMin = 25; // 타이틀 보유 효과 (고정값 최소)
  let titleAccuracyMax = 40; // 타이틀 보유 효과 (고정값 최대)
  let wingAccuracyMin = 20; // 날개 보유 효과 (고정값 최소)
  let wingAccuracyMax = 60; // 날개 보유 효과 (고정값 최대)
  let wingEquipAdditionalAccuracy = 0; // 날개 장착 효과 - 추가 명중 (퍼센트 보너스 적용)
  let wingEquipPveAccuracy = 0; // 날개 장착 효과 - PVE 명중 (퍼센트 보너스 미적용)
  let daevanionArielAccuracy = 0; // 데바니온 아리엘 PVE 명중
  let passiveAccuracy = 0; // 클래스 패시브 스킬 (궁성/마도성/정령성 +100)
  let wildPetAccuracyMin = 0; // 야성 펫작으로 인한 추가 명중 (최소)
  let wildPetAccuracyMax = 0; // 야성 펫작으로 인한 추가 명중 (최대)
  let freedomPercent = 0; // 주신 스탯 자유[바이젤]로 인한 퍼센트
  let precisionPercent = 0; // 일반 스탯 정확으로 인한 퍼센트
  
  // 1. 메인 무기/가더 기본 옵션에서 명중 추출 (정수)
  const weaponAndGauntlet = [...(equipment || [])].filter((item, idx) => {
    let slotPos = -1;
    if (item.slotPos !== undefined && item.slotPos !== null) slotPos = item.slotPos;
    else if (item.slot_pos !== undefined && item.slot_pos !== null) slotPos = item.slot_pos;
    else if (item.slot_index !== undefined && item.slot_index !== null) slotPos = item.slot_index;
    else if (item.slot !== undefined && item.slot !== null) slotPos = item.slot;
    else if (item.raw_data && item.raw_data.slotPos !== undefined && item.raw_data.slotPos !== null) slotPos = item.raw_data.slotPos;
    
    return slotPos == 1 || slotPos == 2 || slotPos == 0 || slotPos == '0' || slotPos == '1' || slotPos == '2';
  });
  
  weaponAndGauntlet.forEach((item) => {
    if (item.main_stats) {
      if (Array.isArray(item.main_stats)) {
        item.main_stats.forEach((stat) => {
          if (typeof stat === 'object' && stat !== null) {
            const statName = String(stat.name || stat.id || '').toLowerCase();
            const statValue = stat.value || stat.minValue || '';
            
            // "명중"이 포함되어 있는지 확인 (추가 명중 제외 - 마석 각인에서 처리)
            if (statName === '명중' || (statName.includes('명중') && !statName.includes('추가 명중') && !statName.includes('다단'))) {
              let accuracyValue = 0;
              
              if (typeof statValue === 'string') {
                const numMatch = statValue.match(/(\d+)/);
                if (numMatch) {
                  accuracyValue = parseInt(numMatch[1]) || 0;
                }
              } else if (typeof statValue === 'number') {
                accuracyValue = parseInt(statValue) || 0;
              }
              
              if (accuracyValue > 0) {
                baseAccuracyMin += accuracyValue;
                baseAccuracyMax += accuracyValue;
              }
            }
          }
        });
      }
    }
  });
  
  // 2. 마석 각인에서 "추가 명중" 추출 (정수)
  // 3. 영혼 각인에서 "명중" 추출 (정수)
  // 3-1. 영혼 각인에서 "민첩" 추출 (야성 펫작 명중 계산용)
  const allItems = [...(equipment || []), ...(accessories || [])];
  let soulAgility = 0; // 영혼 각인에서 민첩 합계
  
  allItems.forEach((item) => {
    if (!item) return;
    
    // 마석 각인 (magic_stone_stat)에서 추가 명중 추출
    if (item.magic_stone_stat) {
      if (Array.isArray(item.magic_stone_stat)) {
        item.magic_stone_stat.forEach((stat) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            // "추가 명중" 패턴 매칭
            if (value > 0 && (name.includes('추가 명중') || name === '추가명중')) {
              stoneAccuracy += value;
            }
          }
        });
      }
    }
    
    // 영혼 각인 (sub_stats)에서 명중 추출
    if (item.sub_stats) {
      if (Array.isArray(item.sub_stats)) {
        item.sub_stats.forEach((stat) => {
          if (typeof stat === 'object') {
            const name = (stat.name || stat.type || '').toLowerCase();
            const value = parseInt(stat.value || stat.amount || 0);
            
            // "명중" 패턴 매칭 (추가 명중, 다단히트 적중 등 제외)
            if (value > 0 && 
                (name === '명중' || name === 'accuracy') &&
                !name.includes('추가') && !name.includes('다단')) {
              soulAccuracy += value;
            }
            
            // "민첩" 패턴 매칭 (야성 펫작 명중 계산용)
            if (value > 0 && (name === '민첩' || name === 'dex' || name === 'dexterity')) {
              soulAgility += value;
            }
          }
        });
      } else if (typeof item.sub_stats === 'object') {
        for (const key of Object.keys(item.sub_stats)) {
          const keyLower = key.toLowerCase();
          const value = parseInt(item.sub_stats[key] || 0);
          
          if (value > 0 && 
              (keyLower === '명중' || keyLower === 'accuracy') &&
              !keyLower.includes('추가') && !keyLower.includes('다단')) {
            soulAccuracy += value;
          }
          
          // "민첩" 패턴 매칭 (야성 펫작 명중 계산용)
          if (value > 0 && (keyLower === '민첩' || keyLower === 'dex' || keyLower === 'dexterity')) {
            soulAgility += value;
          }
        }
      }
    }
  });
  
  // 6. 데바니온 아리엘 보드에서 PVE 명중 추출
  if (daevanionData && daevanionData[45]) {
    const boardData = daevanionData[45];
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node) => {
        let nodeAccuracy = 0;
        
        for (const key in node) {
          const value = node[key];
          
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            // "PVE 명중 +10" 패턴 검색
            let matches = text.match(/PVE\s*명중\s*[+＋]\s*(\d+)/gi);
            if (!matches) {
              matches = text.match(/PVE명중\s*[+＋]\s*(\d+)/gi);
            }
            if (!matches) {
              matches = text.match(/pve\s*accuracy\s*[+＋]\s*(\d+)/gi);
            }
            
            if (matches) {
              matches.forEach(match => {
                const numMatch = match.match(/(\d+)/);
                if (numMatch) {
                  const accuracyValue = parseInt(numMatch[1]) || 0;
                  if (accuracyValue > 0) {
                    nodeAccuracy += accuracyValue;
                  }
                }
              });
            }
          }
          
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/PVE\s*명중\s*[+＋]\s*(\d+)/gi);
                    if (!matches) {
                      matches = itemValue.match(/PVE명중\s*[+＋]\s*(\d+)/gi);
                    }
                    
                    if (matches) {
                      matches.forEach(match => {
                        const numMatch = match.match(/(\d+)/);
                        if (numMatch) {
                          const accuracyValue = parseInt(numMatch[1]) || 0;
                          if (accuracyValue > 0) {
                            nodeAccuracy += accuracyValue;
                          }
                        }
                      });
                    }
                  }
                  
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if (itemName.includes('pve 명중') || itemName.includes('pve명중') || itemName.includes('pve accuracy')) {
                      nodeAccuracy += itemValue;
                    }
                  }
                }
              }
            });
          }
        }
        
        if (nodeAccuracy > 0) {
          daevanionArielAccuracy += nodeAccuracy;
        }
      });
    }
  }
  
  // 6-2. 데바니온 마르쿠탄 보드에서 추가 명중 추출
  let daevanionMarkutanAccuracy = 0;
  if (daevanionData && daevanionData[47]) {
    const boardData = daevanionData[47];
    
    if (boardData && boardData.nodeList) {
      const activeNodes = boardData.nodeList.filter(n => parseInt(n.open || 0) === 1);
      
      activeNodes.forEach((node) => {
        let nodeAccuracy = 0;
        
        for (const key in node) {
          const value = node[key];
          
          if (typeof value === 'string' && value.trim()) {
            const text = value;
            // "추가 명중 +10" 또는 "명중 +10" 패턴 검색 (PVE 명중, 다단히트 적중 제외)
            let matches = text.match(/(?:추가\s*)?명중\s*[+＋]\s*(\d+)/gi);
            if (matches) {
              matches.forEach(match => {
                const matchLower = match.toLowerCase();
                if (matchLower.includes('pve') || matchLower.includes('다단')) return;
                const numMatch = match.match(/(\d+)/);
                if (numMatch) {
                  const accuracyValue = parseInt(numMatch[1]) || 0;
                  if (accuracyValue > 0) {
                    nodeAccuracy += accuracyValue;
                  }
                }
              });
            }
          }
          
          if (Array.isArray(value) && value.length > 0) {
            value.forEach((item) => {
              if (typeof item === 'object' && item !== null) {
                for (const itemKey in item) {
                  const itemValue = item[itemKey];
                  if (typeof itemValue === 'string' && itemValue.trim()) {
                    let matches = itemValue.match(/(?:추가\s*)?명중\s*[+＋]\s*(\d+)/gi);
                    if (matches) {
                      matches.forEach(match => {
                        const matchLower = match.toLowerCase();
                        if (matchLower.includes('pve') || matchLower.includes('다단')) return;
                        const numMatch = match.match(/(\d+)/);
                        if (numMatch) {
                          const accuracyValue = parseInt(numMatch[1]) || 0;
                          if (accuracyValue > 0) {
                            nodeAccuracy += accuracyValue;
                          }
                        }
                      });
                    }
                  }
                  
                  if ((itemKey === 'value' || itemKey === 'amount') && typeof itemValue === 'number' && itemValue > 0) {
                    const itemName = String(item.name || item.desc || item.type || '').toLowerCase();
                    if ((itemName.includes('추가 명중') || itemName.includes('추가명중') || itemName === '명중' || itemName === 'accuracy') &&
                        !itemName.includes('pve') && !itemName.includes('다단')) {
                      nodeAccuracy += itemValue;
                    }
                  }
                }
              }
            });
          }
        }
        
        if (nodeAccuracy > 0) {
          daevanionMarkutanAccuracy += nodeAccuracy;
        }
      });
    }
  }
  
  // 7. 클래스 패시브 스킬 (궁성/마도성/정령성인 경우 +100)
  const currentJob = window.currentJobName || window.currentCharacterJob || '';
  if (currentJob.includes('검성') || currentJob.includes('호법성') || currentJob.includes('궁성') || currentJob.includes('마도성') || currentJob.includes('정령성')) {
    passiveAccuracy = 100;
  }
  
  // 7-2. 야성 펫작으로 인한 추가 명중 (서버에서 계산된 값 사용 - 보안 강화)
  const pureAgility = window.currentCharacterData?.pure_agility || 0;
  wildPetAccuracyMin = window.currentCharacterData?.wild_pet_accuracy_min || 0;
  wildPetAccuracyMax = window.currentCharacterData?.wild_pet_accuracy_max || 65;
  
  // 7-1. 날개 장착 효과로 인한 명중
  const wingEffects = getWingEffects();
  if (wingEffects) {
    // 추가 명중: 퍼센트 보너스 적용 받음
    if (wingEffects.additionalAccuracy) {
      wingEquipAdditionalAccuracy = wingEffects.additionalAccuracy;
    }
    // PVE 명중: 퍼센트 보너스 미적용 (마지막에 더함)
    if (wingEffects.pveAccuracy) {
      wingEquipPveAccuracy = wingEffects.pveAccuracy;
    }
  }
  
  // 7-3. 타이틀 장착 효과에서 명중 추출 (PVE 명중, 추가 명중 구분)
  const currentTitles = window.currentTitles || [];
  if (currentTitles && Array.isArray(currentTitles)) {
    currentTitles.forEach((title) => {
      if (title.equip_effects && Array.isArray(title.equip_effects)) {
        title.equip_effects.forEach((effect) => {
          if (typeof effect === 'string') {
            // "추가 명중 +50" 패턴 매칭 (퍼센트 보너스 적용)
            const additionalAccuracyMatch = effect.match(/추가\s*명중\s*\+?(\d+)/i);
            if (additionalAccuracyMatch) {
              titleEquipAdditionalAccuracy += parseInt(additionalAccuracyMatch[1]) || 0;
            }
            // "PVE 명중 +76" 패턴 매칭 (퍼센트 보너스 미적용)
            const pveAccuracyMatch = effect.match(/PVE\s*명중\s*\+?(\d+)/i);
            if (pveAccuracyMatch) {
              titleEquipPveAccuracy += parseInt(pveAccuracyMatch[1]) || 0;
            }
          }
        });
      }
    });
  }
  
  // 8. 주신 스탯 - 자유[바이젤] 스탯 1당 0.2%
  if (statData && statData.statList) {
    const freedomStat = statData.statList.find(stat => 
      stat.type === 'Freedom' || (stat.name && stat.name.includes('자유'))
    );
    if (freedomStat) {
      const freedomValue = parseInt(freedomStat.value || 0);
      freedomPercent = freedomValue * 0.2;
    }
  }
  
  // 9. 일반 스탯 - 정확 스탯 1당 0.1% (캡 200 적용)
  let precisionValue = 0; // 정확 값 (명중용)
  
  if (statData && statData.statList) {
    const precisionStat = statData.statList.find(stat => 
      stat.type === 'Accuracy' || (stat.name && stat.name.includes('정확'))
    );
    if (precisionStat) {
      precisionValue = parseInt(precisionStat.value || 0);
      const cappedPrecision = Math.min(precisionValue, 200); // 정확 캡 200 적용
      precisionPercent = cappedPrecision * 0.1;
    }
  }
  
  // 퍼센트 영향 받는 정수 명중 합계 (데바니온 아리엘, 마르쿠탄, 날개 PVE 명중, 타이틀 PVE 명중 제외)
  const percentAffectedAccuracyMin = baseAccuracyMin + stoneAccuracy + soulAccuracy + titleEquipAdditionalAccuracy + titleAccuracyMin + wingAccuracyMin + wingEquipAdditionalAccuracy + passiveAccuracy + wildPetAccuracyMin;
  const percentAffectedAccuracyMax = baseAccuracyMax + stoneAccuracy + soulAccuracy + titleEquipAdditionalAccuracy + titleAccuracyMax + wingAccuracyMax + wingEquipAdditionalAccuracy + passiveAccuracy + wildPetAccuracyMax;
  
  // 퍼센트 합계 계산
  totalPercentAccuracy = freedomPercent + precisionPercent;
  
  // 최종 명중 계산 (퍼센트 영향 받는 명중 * (1 + 퍼센트/100) + 데바니온 아리엘 + 마르쿠탄 추가 명중 + 날개 PVE 명중 + 타이틀 PVE 명중)
  const finalAccuracyMin = Math.floor(percentAffectedAccuracyMin * (1 + totalPercentAccuracy / 100)) + daevanionArielAccuracy + daevanionMarkutanAccuracy + wingEquipPveAccuracy + titleEquipPveAccuracy;
  const finalAccuracyMax = Math.floor(percentAffectedAccuracyMax * (1 + totalPercentAccuracy / 100)) + daevanionArielAccuracy + daevanionMarkutanAccuracy + wingEquipPveAccuracy + titleEquipPveAccuracy;
  
  // 총 정수 명중 (표시용)
  totalIntegerAccuracyMin = percentAffectedAccuracyMin + daevanionArielAccuracy + daevanionMarkutanAccuracy + wingEquipPveAccuracy + titleEquipPveAccuracy;
  totalIntegerAccuracyMax = percentAffectedAccuracyMax + daevanionArielAccuracy + daevanionMarkutanAccuracy + wingEquipPveAccuracy + titleEquipPveAccuracy;
  
  const result = {
    finalAccuracyMin: finalAccuracyMin,
    finalAccuracyMax: finalAccuracyMax,
    totalIntegerAccuracyMin: totalIntegerAccuracyMin,
    totalIntegerAccuracyMax: totalIntegerAccuracyMax,
    totalPercentAccuracy: totalPercentAccuracy,
    breakdown: {
      baseAccuracyMin: baseAccuracyMin,
      baseAccuracyMax: baseAccuracyMax,
      stoneAccuracy: stoneAccuracy,
      soulAccuracy: soulAccuracy,
      titleEquipAdditionalAccuracy: titleEquipAdditionalAccuracy,
      titleEquipPveAccuracy: titleEquipPveAccuracy,
      titleAccuracyMin: titleAccuracyMin,
      titleAccuracyMax: titleAccuracyMax,
      wingAccuracyMin: wingAccuracyMin,
      wingAccuracyMax: wingAccuracyMax,
      wingEquipAdditionalAccuracy: wingEquipAdditionalAccuracy,
      wingEquipPveAccuracy: wingEquipPveAccuracy,
      daevanionArielAccuracy: daevanionArielAccuracy,
      daevanionMarkutanAccuracy: daevanionMarkutanAccuracy,
      passiveAccuracy: passiveAccuracy,
      wildPetAccuracyMin: wildPetAccuracyMin,
      wildPetAccuracyMax: wildPetAccuracyMax,
      freedomPercent: freedomPercent,
      precisionPercent: precisionPercent,
      pureAgility: pureAgility  // 야성 펫작 완료율 계산용
    }
  };
  
  // 전역 변수에 저장
  window.accuracyResult = result;
  
  // 명중 표시 업데이트
  displayAccuracyStats(result);
  
  return result;
}

async function calculateSkillDamage(skills, stigmas) {
  
  let totalSkillDamage = 0; // 총 스킬 점수
  let activeSkillDamage = 0; // 액티브 스킬 점수
  let passiveSkillDamage = 0; // 패시브 스킬 점수
  let stigmaSkillDamage = 0; // 스티그마 스킬 점수
  
  // 채용 가능한 스킬 개수
  const MAX_ACTIVE_SKILLS = 12;
  const MAX_PASSIVE_SKILLS = 10;
  const MAX_STIGMA_SKILLS = 11;
  
  // 현재 캐릭터의 직업 가져오기
  const currentJob = window.currentCharacterJob || null;
  
  if (!currentJob) {
    console.warn('[스킬 점수 계산] 직업 정보가 없습니다.');
    const result = {
      totalSkillDamage: 0,
      breakdown: {
        activeSkillDamage: 0,
        passiveSkillDamage: 0,
        stigmaSkillDamage: 0
      },
      details: {
        active: [],
        passive: [],
        stigma: []
      }
    };
    window.skillDamageResult = result;
    displaySkillDamageStats(result);
    return result;
  }
  
  // 스킬 우선순위 데이터 가져오기 (캐릭터 검색 시 이미 로드됨)
  let skillPriorities = window.currentSkillPriorities;
  
  if (!skillPriorities) {
    console.warn('[스킬 점수 계산] 스킬 우선순위 데이터가 없습니다.');
    skillPriorities = { active: [], passive: [], stigma: [] };
  }
  
  // ========================================================================
  // 클래스별 액티브 스킬 DPS 지분 weight 데이터 (미터기 기반)
  // - 주요 딜링 스킬에 실제 딜 지분(%) 부여
  // - 명시되지 않은 나머지 스킬은 잔여 % 균등 분배
  // ========================================================================
  const ACTIVE_SKILL_DPS_WEIGHTS = {
    '검성': { '내려찍기': 50, '절단의 맹타': 30, '파멸의 맹타': 10 },
    '살성': { '심장 찌르기': 25, '문양 폭발': 15, '기습': 15, '빠른 베기': 15, '맹수의 포효': 15 },
    '궁성': { '속사': 35, '저격': 25, '조준 화살': 10, '광풍 화살': 6, '송곳 화살': 6 },
    '정령성': { '화염 전소': 44, '냉기 충격': 26, '원소 융합': 20 },
    '수호성': { '연속 난타': 20, '맹렬한 일격': 30, '심판': 40 },
    '마도성': { '혹한의 바람': 18, '불꽃 폭발': 20, '불꽃 화살': 20, '집중의 기원': 15, '얼음 사슬': 10 },
    '호법성': { '암격쇄': 35, '격파쇄': 25, '백열격': 10, '회전격': 8, '쾌유의 주문' : 10 },
    '치유성': { '쾌유의 광휘': 30, '심판의 번개': 30, '치유의 빛': 10, '재생의 빛': 10, '단죄': 10 }
  };

  // ========================================================================
  // 클래스별 패시브 스킬 DPS 지분 weight 데이터 (미터기 기반)
  // - 주요 패시브 스킬에 실제 딜 기여도(%) 부여
  // - 명시되지 않은 나머지 스킬은 잔여 % 균등 분배
  // ========================================================================
  const PASSIVE_SKILL_DPS_WEIGHTS = {
    '검성': { '공격 준비': 20, '충격 적중': 20, '약점 파악': 15, '노련한 반격': 15 },
    '수호성': { '격앙': 25, '충격 적중': 20, '철벽 방어': 15 },
    '살성': { '강습 자세': 25, '배후 강타': 25, '빈틈 노리기': 20, '충격 적중': 20 },
    '궁성': { '집중의 눈': 25, '사냥꾼의 결의': 25, '사냥꾼의 혼': 20 },
    '마도성': { '불꽃의 로브': 30, '불의 표식': 25, '생기 증발': 15, '냉기 소환': 15 },
    '정령성': { '정령 타격': 30, '정신 집중': 25, '침식': 15 },
    '치유성': { '대지의 은총': 25, '치유력 강화': 20, '주신의 은총': 15 },
    '호법성': { '공격 준비': 25, '충격 적중': 20, '고취의 주문': 15 }
  };

  // 스킬 DPS weight 기반 multiplier 계산 함수 (공용)
  // - 주요 스킬: DPS 지분 그대로 weight
  // - 나머지 스킬: 잔여 지분 균등 분배
  // - multiplier = (weight% / 100) * maxSlots → 총합 시 /maxSlots과 상쇄되어 DPS 지분 비율 반영
  function getSkillDpsMultiplier(skillName, classJob, maxSlots, weightTable) {
    if (!classJob || !weightTable[classJob]) {
      return 1.0;  // 데이터 없는 클래스는 기존 방식
    }
    const dpsWeights = weightTable[classJob];
    const totalKeyWeight = Object.values(dpsWeights).reduce((s, v) => s + v, 0);
    const keySkillCount = Object.keys(dpsWeights).length;
    const nonKeyCount = maxSlots - keySkillCount;
    const remainingWeight = 100 - totalKeyWeight;
    const nonKeyWeight = nonKeyCount > 0 ? remainingWeight / nonKeyCount : 0;
    const skillWeight = (dpsWeights[skillName] !== undefined) ? dpsWeights[skillName] : nonKeyWeight;
    return (skillWeight / 100) * maxSlots;
  }

  // 스티그마 위치 기반 가중치 (1번째=100%, 2번째=100%, 3번째=70%, 4번째=50%)
  const STIGMA_POSITION_WEIGHTS = [1.0, 1.0, 0.7, 0.5];

  // 스킬 점수 계산 함수 (액티브: DPS 지분 weight 적용, 그 외: 동일 weight 100%)
  function calculateSkillScore(skillList, priorityList, maxSlots, skillType) {
    const details = [];
    let totalScore = 0;
    
    // 캐릭터가 보유한 스킬을 맵으로 변환
    const skillMap = {};
    skillList.forEach(skill => {
      const skillName = skill.name || '';
      const skillLevel = skill.level_int || parseInt(skill.level || '0', 10) || 0;
      if (skillLevel > 0) {
        skillMap[skillName] = skillLevel;
      }
    });
    
    // 채용률 10% 미만인 스킬 제외 (잘못된 데이터 필터링 - 스킬 목록 표시용)
    const filteredPriorityList = priorityList.filter(p => (p.adoption_rate || 0) >= 10);
    
    // 스킬 타입별로 계산에 포함할 스킬 결정
    // 액티브: DB 상위 12개 (안 찍은 스킬은 0점)
    // 패시브: DB 상위 10개 (안 찍은 스킬은 0점)
    // 스티그마: 캐릭터가 찍은 스킬 중 레벨 높은 4개
    let effectivePriorityList = filteredPriorityList;
    if (skillType === 'stigma') {
      // 캐릭터가 찍은 스킬 중 레벨이 높은 순으로 정렬하여 상위 4개 선택
      const characterStigmas = filteredPriorityList
        .map(p => ({
          ...p,
          characterLevel: skillMap[p.skill_name] || 0
        }))
        .filter(p => p.characterLevel > 0)
        .sort((a, b) => {
          // 캐릭터 레벨 높은 순으로 정렬
          if (b.characterLevel !== a.characterLevel) {
            return b.characterLevel - a.characterLevel;
          }
          // 레벨이 같으면 DB priority 순
          return (a.priority || 0) - (b.priority || 0);
        })
        .slice(0, 4);  // 레벨 높은 상위 4개
      
      effectivePriorityList = characterStigmas;
    } else {
      // 액티브/패시브: DB 상위 maxSlots개 모두 (안 찍은 스킬은 0점 처리)
      effectivePriorityList = filteredPriorityList.slice(0, maxSlots);
    }
    
    // 우선순위 목록을 순회하며 점수 계산
    effectivePriorityList.forEach((prioritySkill, index) => {
      const skillName = prioritySkill.skill_name;
      // 스티그마는 DB의 원래 priority 사용, 그 외는 index+1 사용
      const priority = (skillType === 'stigma') ? (prioritySkill.priority || (index + 1)) : (index + 1);
      const adoptionRate = prioritySkill.adoption_rate;
      const averageLevel = prioritySkill.average_level;
      const characterSkillLevel = skillMap[skillName] || 0;
      
      if (characterSkillLevel === 0) {
        details.push({
          skillName: skillName,
          priority: priority,
          adoptionRate: adoptionRate,
          averageLevel: averageLevel,
          characterLevel: 0,
          baseScore: 0,
          bonusScore: 0,
          multiplier: 1.0,
          finalScore: 0,
          icon: prioritySkill.skill_icon
        });
        return;
      }
      
      // 1. 기본 점수 계산 (모든 스킬 타입 레벨당 1.35%)
      let baseScore = characterSkillLevel * 1.35;
      
      // 2. 보너스 점수 계산
      let bonusScore = 0;
      
      if (skillType === 'active') {
        // 액티브: 8/12/16/20 레벨 달성 시 +5/10/15/10%
        if (characterSkillLevel >= 8) bonusScore += 5;
        if (characterSkillLevel >= 12) bonusScore += 10;
        if (characterSkillLevel >= 16) bonusScore += 15;
        if (characterSkillLevel >= 20) bonusScore += 10;
      } else if (skillType === 'stigma') {
        // 스티그마: 5/10/15/20 레벨 달성 시 +5/10/20/40%
        if (characterSkillLevel >= 5) bonusScore += 5;
        if (characterSkillLevel >= 10) bonusScore += 10;
        if (characterSkillLevel >= 15) bonusScore += 20;
        if (characterSkillLevel >= 20) bonusScore += 40;
      }
      // 패시브: 보너스 없음 (레벨당 1.35%만)
      
      // 3. Weight 적용 (액티브/패시브: DPS 지분 weight, 스티그마: 위치 기반 가중치)
      let multiplier = 1.0;
      if (skillType === 'active') {
        multiplier = getSkillDpsMultiplier(skillName, currentJob, maxSlots, ACTIVE_SKILL_DPS_WEIGHTS);
      } else if (skillType === 'passive') {
        multiplier = getSkillDpsMultiplier(skillName, currentJob, maxSlots, PASSIVE_SKILL_DPS_WEIGHTS);
      } else if (skillType === 'stigma') {
        multiplier = STIGMA_POSITION_WEIGHTS[index] !== undefined ? STIGMA_POSITION_WEIGHTS[index] : 0.5;
      }
      
      // 4. 최종 점수: (기본 점수 + 보너스 점수) × weight
      const skillScore = (baseScore + bonusScore) * multiplier;
      totalScore += skillScore;
      
      const detailEntry = {
        skillName: skillName,
        priority: priority,
        adoptionRate: adoptionRate,
        averageLevel: averageLevel,
        characterLevel: characterSkillLevel,
        baseScore: baseScore,
        bonusScore: bonusScore,
        multiplier: multiplier,
        finalScore: skillScore,
        icon: prioritySkill.skill_icon
      };
      
      details.push(detailEntry);
    });
    
    // 최종 점수: 총합을 maxSlots로 나눔
    const finalScore = totalScore / maxSlots;
    
    return { score: finalScore, details: details, totalBeforeDivide: totalScore };
  }
  
  // 스킬 상세 정보 저장
  let activeDetails = [];
  let passiveDetails = [];
  let stigmaDetails = [];
  
  // 나누기 전의 총합을 저장할 변수
  let activeTotalBeforeDivide = 0;
  let passiveTotalBeforeDivide = 0;
  let stigmaTotalBeforeDivide = 0;
  
  // 1. 액티브 스킬 점수 계산
  if (Array.isArray(skills) && skills.length > 0) {
    const activeSkills = skills.filter(skill => {
      const group = detectSkillGroup(skill);
      return group === 'active';
    });
    
    const activeResult = calculateSkillScore(
      activeSkills,
      skillPriorities.active || [],
      MAX_ACTIVE_SKILLS,
      'active'
    );
    activeSkillDamage = activeResult.score;  // 개별 표시용 (12로 나눠진 값)
    activeTotalBeforeDivide = activeResult.totalBeforeDivide || 0;  // 나누기 전 총합
    activeDetails = activeResult.details;
  }
  
  // 2. 패시브 스킬 점수 계산
  if (Array.isArray(skills) && skills.length > 0) {
    const passiveSkills = skills.filter(skill => {
      const group = detectSkillGroup(skill);
      return group === 'passive';
    });
    
    const passiveResult = calculateSkillScore(
      passiveSkills,
      skillPriorities.passive || [],
      MAX_PASSIVE_SKILLS,
      'passive'
    );
    passiveSkillDamage = passiveResult.score;  // 개별 표시용 (10으로 나눠진 값)
    passiveTotalBeforeDivide = passiveResult.totalBeforeDivide || 0;  // 나누기 전 총합
    passiveDetails = passiveResult.details;
  }
  
  // 3. 스티그마 스킬 점수 계산
  if (Array.isArray(stigmas) && stigmas.length > 0) {
    const stigmaResult = calculateSkillScore(
      stigmas,
      skillPriorities.stigma || [],
      4,  // 스티그마는 상위 4개만 계산하므로 4로 나눔
      'stigma'
    );
    stigmaSkillDamage = stigmaResult.score;  // 개별 표시용 (4로 나눠진 값)
    stigmaTotalBeforeDivide = stigmaResult.totalBeforeDivide || 0;  // 나누기 전 총합
    stigmaDetails = stigmaResult.details;
  }
  
  // 총 스킬 점수 계산 (가중치는 이미 중요도 계수에 반영되어 있음)
  totalSkillDamage = activeSkillDamage + passiveSkillDamage + stigmaSkillDamage;
  
  const result = {
    totalSkillDamage: totalSkillDamage,
    breakdown: {
      activeSkillDamage: activeSkillDamage,
      passiveSkillDamage: passiveSkillDamage,
      stigmaSkillDamage: stigmaSkillDamage
    },
    details: {
      active: activeDetails,
      passive: passiveDetails,
      stigma: stigmaDetails
    }
  };
  
  // 전역 변수에 저장
  window.skillDamageResult = result;
  
  // 스킬 딜증 표시 업데이트
  displaySkillDamageStats(result);
  
  return result;
}

function calculateSkillScore(skillList, priorityList, maxSlots, skillType) {
    const details = [];
    let totalScore = 0;
    
    // 캐릭터가 보유한 스킬을 맵으로 변환
    const skillMap = {};
    skillList.forEach(skill => {
      const skillName = skill.name || '';
      const skillLevel = skill.level_int || parseInt(skill.level || '0', 10) || 0;
      if (skillLevel > 0) {
        skillMap[skillName] = skillLevel;
      }
    });
    
    // 채용률 10% 미만인 스킬 제외 (잘못된 데이터 필터링 - 스킬 목록 표시용)
    const filteredPriorityList = priorityList.filter(p => (p.adoption_rate || 0) >= 10);
    
    // 스킬 타입별로 계산에 포함할 스킬 결정
    // 액티브: DB 상위 12개 (안 찍은 스킬은 0점)
    // 패시브: DB 상위 10개 (안 찍은 스킬은 0점)
    // 스티그마: 캐릭터가 찍은 스킬 중 레벨 높은 4개
    let effectivePriorityList = filteredPriorityList;
    if (skillType === 'stigma') {
      // 캐릭터가 찍은 스킬 중 레벨이 높은 순으로 정렬하여 상위 4개 선택
      const characterStigmas = filteredPriorityList
        .map(p => ({
          ...p,
          characterLevel: skillMap[p.skill_name] || 0
        }))
        .filter(p => p.characterLevel > 0)
        .sort((a, b) => {
          // 캐릭터 레벨 높은 순으로 정렬
          if (b.characterLevel !== a.characterLevel) {
            return b.characterLevel - a.characterLevel;
          }
          // 레벨이 같으면 DB priority 순
          return (a.priority || 0) - (b.priority || 0);
        })
        .slice(0, 4);  // 레벨 높은 상위 4개
      
      effectivePriorityList = characterStigmas;
    } else {
      // 액티브/패시브: DB 상위 maxSlots개 모두 (안 찍은 스킬은 0점 처리)
      effectivePriorityList = filteredPriorityList.slice(0, maxSlots);
    }
    
    // 우선순위 목록을 순회하며 점수 계산
    effectivePriorityList.forEach((prioritySkill, index) => {
      const skillName = prioritySkill.skill_name;
      // 스티그마는 DB의 원래 priority 사용, 그 외는 index+1 사용
      const priority = (skillType === 'stigma') ? (prioritySkill.priority || (index + 1)) : (index + 1);
      const adoptionRate = prioritySkill.adoption_rate;
      const averageLevel = prioritySkill.average_level;
      const characterSkillLevel = skillMap[skillName] || 0;
      
      if (characterSkillLevel === 0) {
        details.push({
          skillName: skillName,
          priority: priority,
          adoptionRate: adoptionRate,
          averageLevel: averageLevel,
          characterLevel: 0,
          baseScore: 0,
          bonusScore: 0,
          multiplier: 1.0,
          finalScore: 0,
          icon: prioritySkill.skill_icon
        });
        return;
      }
      
      // 1. 기본 점수 계산 (모든 스킬 타입 레벨당 1.35%)
      let baseScore = characterSkillLevel * 1.35;
      
      // 2. 보너스 점수 계산
      let bonusScore = 0;
      
      if (skillType === 'active') {
        // 액티브: 8/12/16/20 레벨 달성 시 +5/10/15/10%
        if (characterSkillLevel >= 8) bonusScore += 5;
        if (characterSkillLevel >= 12) bonusScore += 10;
        if (characterSkillLevel >= 16) bonusScore += 15;
        if (characterSkillLevel >= 20) bonusScore += 10;
      } else if (skillType === 'stigma') {
        // 스티그마: 5/10/15/20 레벨 달성 시 +5/10/20/40%
        if (characterSkillLevel >= 5) bonusScore += 5;
        if (characterSkillLevel >= 10) bonusScore += 10;
        if (characterSkillLevel >= 15) bonusScore += 20;
        if (characterSkillLevel >= 20) bonusScore += 40;
      }
      // 패시브: 보너스 없음 (레벨당 1.35%만)
      
      // 3. Weight 적용 (액티브/패시브: DPS 지분 weight, 스티그마: 위치 기반 가중치)
      let multiplier = 1.0;
      if (skillType === 'active') {
        multiplier = getSkillDpsMultiplier(skillName, currentJob, maxSlots, ACTIVE_SKILL_DPS_WEIGHTS);
      } else if (skillType === 'passive') {
        multiplier = getSkillDpsMultiplier(skillName, currentJob, maxSlots, PASSIVE_SKILL_DPS_WEIGHTS);
      } else if (skillType === 'stigma') {
        multiplier = STIGMA_POSITION_WEIGHTS[index] !== undefined ? STIGMA_POSITION_WEIGHTS[index] : 0.5;
      }
      
      // 4. 최종 점수: (기본 점수 + 보너스 점수) × weight
      const skillScore = (baseScore + bonusScore) * multiplier;
      totalScore += skillScore;
      
      const detailEntry = {
        skillName: skillName,
        priority: priority,
        adoptionRate: adoptionRate,
        averageLevel: averageLevel,
        characterLevel: characterSkillLevel,
        baseScore: baseScore,
        bonusScore: bonusScore,
        multiplier: multiplier,
        finalScore: skillScore,
        icon: prioritySkill.skill_icon
      };
      
      details.push(detailEntry);
    });
    
    // 최종 점수: 총합을 maxSlots로 나눔
    const finalScore = totalScore / maxSlots;
    
    return { score: finalScore, details: details, totalBeforeDivide: totalScore };
  }

function calculateSkillBreakdown(skillName) {
  // 안전성 체크: skillName이 없으면 빈 breakdown 반환
  if (!skillName || typeof skillName !== 'string') {
    return { arcana: 0, equipment: 0, base: 0 };
  }
  
  const breakdown = {
    arcana: 0,
    equipment: 0,
    base: 0
  };
  
  try {
    // 1. 아르카나에서 스킬 포인트 수집
    const allItems = [...(window.currentEquipment || []), ...(window.currentAccessories || [])];
    
    allItems.forEach(item => {
      if (!item || typeof item !== 'object') return;
      
      if (isArcanaItem(item) && item.sub_skills && Array.isArray(item.sub_skills)) {
        item.sub_skills.forEach(subSkill => {
          if (!subSkill || typeof subSkill !== 'object') return;
          
          const subSkillName = subSkill.name || '';
          if (!subSkillName) return;
          
          try {
            if (subSkillName === skillName || extractBaseSkillName(subSkillName) === extractBaseSkillName(skillName)) {
              const level = parseInt(subSkill.level || subSkill.level_int || 0, 10);
              if (!isNaN(level) && level > 0) {
                breakdown.arcana += level;
              }
            }
          } catch (e) {
            // extractBaseSkillName 에러 무시하고 계속 진행
          }
        });
      }
    });
    
    // 2. 장비/장신구 부스킬에서 스킬 포인트 수집
    allItems.forEach(item => {
      if (!item || typeof item !== 'object') return;
      
      if (!isArcanaItem(item) && item.sub_skills && Array.isArray(item.sub_skills)) {
        item.sub_skills.forEach(subSkill => {
          if (!subSkill || typeof subSkill !== 'object') return;
          
          const subSkillName = subSkill.name || '';
          if (!subSkillName) return;
          
          try {
            if (subSkillName === skillName || extractBaseSkillName(subSkillName) === extractBaseSkillName(skillName)) {
              const level = parseInt(subSkill.level || subSkill.level_int || 0, 10);
              if (!isNaN(level) && level > 0) {
                breakdown.equipment += level;
              }
            }
          } catch (e) {
            // extractBaseSkillName 에러 무시하고 계속 진행
          }
        });
      }
    });
  } catch (e) {
    // 전체 에러 발생 시 빈 breakdown 반환
    return { arcana: 0, equipment: 0, base: 0 };
  }
  
  return breakdown;
}

function detectSkillGroup(skill) {
  if (!skill) return 'active';
  const group = (skill.group || skill.skill_group || '').toString().toLowerCase();
  if (group.includes('passive') || group.includes('패시브')) return 'passive';
  if (group.includes('active') || group.includes('액티브')) return 'active';
  const typeText = (skill.type || '').toString().toLowerCase();
  if (typeText.includes('passive') || typeText.includes('패시브')) return 'passive';
  return 'active';
}

function extractBaseSkillName(skillName) {
  if (!skillName) return '';
  // 화살표(→) 또는 화살표 유사 문자 제거
  const arrowPattern = /[→→→?]/;
  if (arrowPattern.test(skillName)) {
    return skillName.split(arrowPattern)[0].trim();
  }
  return skillName.trim();
}

function isNewMetaSkill(skillName, rankIndex, usageRate15) {
  if (NEW_META_EXCLUDE_SKILLS.includes(skillName)) return false;
  if (rankIndex < 4) return false;  // 1~4순위는 이미 메타
  if ((usageRate15 || 0) < 0.1) return false;  // 15렙 이상 채용률 10% 미만은 제외
  return true;
}

function getSkillDpsMultiplier(skillName, classJob, maxSlots, weightTable) {
    if (!classJob || !weightTable[classJob]) {
      return 1.0;  // 데이터 없는 클래스는 기존 방식
    }
    const dpsWeights = weightTable[classJob];
    const totalKeyWeight = Object.values(dpsWeights).reduce((s, v) => s + v, 0);
    const keySkillCount = Object.keys(dpsWeights).length;
    const nonKeyCount = maxSlots - keySkillCount;
    const remainingWeight = 100 - totalKeyWeight;
    const nonKeyWeight = nonKeyCount > 0 ? remainingWeight / nonKeyCount : 0;
    const skillWeight = (dpsWeights[skillName] !== undefined) ? dpsWeights[skillName] : nonKeyWeight;
    return (skillWeight / 100) * maxSlots;
  }

function getSoulInscriptionTier(optionName) {
  if (!optionName) return 'C';
  
  const name = String(optionName).trim();
  
  // 주신 스탯이 포함된 경우 등급 판단 (장비/장신구 영혼각인에 주신 스탯이 붙는 경우)
  // S 등급 주신 스탯
  const sTierGodStones = [
    '환상[카이시넬]',
    '시간[시엘]',
    '파괴[지켈]',
    '죽음[트리니엘]',
    '자유[바이젤]',
    '지혜[루미엘]'
  ];
  
  // A 등급 주신 스탯
  const aTierGodStones = [
    '정의[네자칸]',
    '공간[이스라펠]'
  ];
  
  // 주신 스탯이 포함되어 있는지 확인
  for (const godStone of sTierGodStones) {
    if (name.includes(godStone)) {
      return 'S';
    }
  }
  
  for (const godStone of aTierGodStones) {
    if (name.includes(godStone)) {
      return 'A';
    }
  }
  
  // S 등급 (필수)
  const sTierOptions = [
    '무기 피해 증폭',
    '전투 속도',
    '피해 증폭',
    '치명타 피해 증폭',
    '위력',
    '다단 히트 적중',
    '정확'
  ];
  
  // A 등급 (유효)
  const aTierOptions = [
    '이동 속도',
    '공격력',
    '공격력 증가',
    '강타',
    '치명타',
    '명중'
  ];
  
  // B 등급 (애매)
  const bTierOptions = [
    '막기',
    '비행력',
    '회피',
    '생명력',
    '최대 생명력',
    '피해 내성',
    '방어력',
    '치명타 방어력',
    '후방 공격력',
    '정신력',
    '치명타 저항',
    '강타 저항',
    '완벽 저항',
    '상태이상 저항',
    '상태이상 적중',
    '철벽 관통',
    '재생 관통',
    '재생',
    '철벽'
  ];
  
  // S 등급 확인 (옵션 이름에 포함되어 있는지 확인)
  for (const sOption of sTierOptions) {
    if (name.includes(sOption)) {
      return 'S';
    }
  }
  
  // B 등급 확인 (A 등급보다 먼저 체크하여 "치명타 방어력", "치명타 저항" 등을 정확히 분류)
  for (const bOption of bTierOptions) {
    if (name.includes(bOption)) {
      return 'B';
    }
  }
  
  // A 등급 확인 - "치명타"는 정확히 일치하거나 "치명타 "로 시작하는 경우만 (치명타 방어력, 치명타 저항 제외)
  for (const aOption of aTierOptions) {
    if (aOption === '치명타') {
      // "치명타"가 정확히 일치하거나 "치명타 "로 시작하되 "치명타 방어력"이나 "치명타 저항"이 아닌 경우
      if (name === '치명타' || (name.startsWith('치명타 ') && !name.includes('치명타 방어력') && !name.includes('치명타 저항'))) {
        return 'A';
      }
    } else if (name.includes(aOption)) {
      return 'A';
    }
  }
  
  // 기본값: B 등급 (나머지 모든 옵션)
  return 'B';
}